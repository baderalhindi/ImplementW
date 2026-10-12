using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Tests.Unit.Application.AuditActivity;

/// <summary>TASK-073 (AUD-CC-27): the verifier finds a changed row, a removed event and a changed attribute; the database's own run is in the integration suite.</summary>
public sealed class AuditIntegrityVerifierTests
{
    private readonly Chain _chain = new();

    [Fact]
    public async Task AnIntactChainHasNoFinding()
    {
        _chain.Append(3);

        AuditIntegrityReport report = await Verify();

        Assert.True(report.Intact);
        Assert.Equal((3, 0), (report.EventsVerified, report.EventsWithoutDigest));
    }

    [Fact]
    public async Task AChangedRowBreaksItsLink()
    {
        _chain.Append(3);
        _chain.Links[1] = _chain.Links[1] with { RecomputedHash = new string('f', 64) };

        AuditIntegrityReport report = await Verify();

        Assert.Equal([new AuditIntegrityFinding(_chain.Links[1].AuditEventId, AuditIntegrityFindingKind.BrokenLink)], report.Findings);
    }

    [Fact]
    public async Task ARemovedEventBreaksTheLinkAfterIt()
    {
        _chain.Append(3);
        Guid after = _chain.Links[2].AuditEventId;
        _chain.Links.RemoveAt(1);

        AuditIntegrityReport report = await Verify();

        Assert.Equal([new AuditIntegrityFinding(after, AuditIntegrityFindingKind.BrokenLink)], report.Findings);
    }

    [Fact]
    public async Task AChangedAttributeNoLongerHasItsDigest()
    {
        _chain.Append(2);
        Guid changed = _chain.Links[0].AuditEventId;
        _chain.Attributes[0].NewValue = "TAMPERED";

        AuditIntegrityReport report = await Verify();

        Assert.Equal([new AuditIntegrityFinding(changed, AuditIntegrityFindingKind.AttributeDigestMismatch)], report.Findings);
        Assert.Equal((0, 1), (report.BrokenLinks, report.AttributeDigestMismatches));
    }

    [Fact]
    public async Task AnEventStoredBeforeTheDigestIsCountedAsUncovered()
    {
        _chain.Append(2);
        _chain.Links[0] = _chain.Links[0] with { AttributeDigest = null };

        AuditIntegrityReport report = await Verify();

        Assert.True(report.Intact);
        Assert.Equal(1, report.EventsWithoutDigest);
    }

    private Task<AuditIntegrityReport> Verify() => new AuditIntegrityVerifier(_chain).VerifyAsync(CancellationToken.None);

    /// <summary>A chain whose links hash consistently, each with one attribute.</summary>
    private sealed class Chain : IAuditIntegrityRepository
    {
        private static readonly DateTimeOffset Start = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

        public List<AuditChainLink> Links { get; } = [];

        public List<AuditEventAttribute> Attributes { get; } = [];

        public void Append(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Guid id = Guid.NewGuid();
                AuditEventAttribute attribute = new() { Id = Guid.NewGuid(), AuditEventId = id, AttributeName = "status", NewValue = $"S{i}" };
                Attributes.Add(attribute);
                string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(id.ToByteArray()));
                Links.Add(new AuditChainLink(id, Start.AddSeconds(Links.Count), hash, Links.Count == 0 ? null : Links[^1].EventHash, hash, AuditAttributeDigest.Of([attribute])));
            }
        }

        public Task<IReadOnlyList<AuditChainLink>> ReadChainAsync(AuditStorePosition? after, int count, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditChainLink>>([.. Links.Where(l => after is null || l.RecordedAt > after.RecordedAt).Take(count)]);

        public Task<IReadOnlyList<AuditEventAttribute>> ReadAttributesAsync(IReadOnlyCollection<Guid> auditEventIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditEventAttribute>>([.. Attributes.Where(a => auditEventIds.Contains(a.AuditEventId))]);

        public Task<AuditIngestionStatus> GetStatusAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
