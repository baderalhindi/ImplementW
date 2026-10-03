using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Milestone;

namespace PMPlatform.Tests.Unit.Application.Milestone;

/// <summary>
/// PTBC-006: the evidence a milestone's category makes mandatory is EVIDENCE_POLICY's, enforced as published; with no version in
/// force nothing is mandatory yet, and anything else unresolvable fails closed.
/// </summary>
public sealed class MilestoneEvidencePolicyTests
{
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly Guid Handover = Guid.NewGuid();
    private static readonly Guid Design = Guid.NewGuid();
    private static readonly Guid Certificate = Guid.NewGuid();
    private static readonly Guid Photos = Guid.NewGuid();
    private static readonly Guid Minutes = Guid.NewGuid();

    [Fact]
    public async Task TheMandatoryEvidenceIsTheCategorysMandatoryRowsWithTheVersionThatHoldsThem()
    {
        EvidenceRequirement requirement = await Policy(
                new EvidenceRequirementEntry(Handover, Certificate, IsMandatory: true),
                new EvidenceRequirementEntry(Handover, Photos, IsMandatory: false),
                new EvidenceRequirementEntry(Handover, Minutes, IsMandatory: true),
                new EvidenceRequirementEntry(Design, Photos, IsMandatory: true))
            .RequirementAsync(Handover, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(VersionId, requirement.PolicyVersionId);
        Assert.Equal([Certificate, Minutes], requirement.Mandatory);
    }

    [Fact]
    public async Task ACategoryWithNoRowsHasNoMandatoryEvidenceUnderAPublishedPolicy()
    {
        EvidenceRequirement requirement = await Policy(new EvidenceRequirementEntry(Design, Photos, IsMandatory: true))
            .RequirementAsync(Handover, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal((VersionId, 0), (requirement.PolicyVersionId, requirement.Mandatory.Count));
    }

    [Fact]
    public async Task WithNoPolicyInForceNothingIsMandatoryYet()
    {
        MilestoneEvidencePolicy policy = new(new StubResolver(null, ConfigurationMissingReason.NoEffectiveVersion));

        Assert.Same(EvidenceRequirement.PendingPolicy, await policy.RequirementAsync(Handover, DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Theory]
    [InlineData(ConfigurationMissingReason.AmbiguousVersions)]
    [InlineData(ConfigurationMissingReason.UnknownCode)]
    public async Task AnUnresolvablePolicyFailsClosed(ConfigurationMissingReason reason)
    {
        MilestoneEvidencePolicy policy = new(new StubResolver(null, reason));

        Assert.Equal(reason, (await Assert.ThrowsAsync<ConfigurationMissingException>(() => policy.RequirementAsync(Handover, DateTimeOffset.UtcNow, CancellationToken.None))).Reason);
    }

    [Fact]
    public void WhatIsMissingIsTheMandatoryTypesNotSatisfied()
    {
        EvidenceRequirement requirement = new(VersionId, [Certificate, Minutes]);

        Assert.Equal([Minutes], requirement.MissingFrom(new HashSet<Guid> { Certificate, Photos }));
        Assert.Empty(requirement.MissingFrom(new HashSet<Guid> { Certificate, Minutes }));
        Assert.Empty(EvidenceRequirement.PendingPolicy.MissingFrom(new HashSet<Guid>()));
    }

    private static MilestoneEvidencePolicy Policy(params EvidenceRequirementEntry[] rows) =>
        new(new StubResolver(new ConfigurationContent { EvidenceRequirements = rows }));

    /// <summary>Resolves to <paramref name="content"/>, or fails for <paramref name="missing"/>.</summary>
    private sealed class StubResolver(ConfigurationContent? content, ConfigurationMissingReason? missing = null) : IConfigurationResolver
    {
        public Task<ResolvedConfiguration> ResolveAsync(string familyCode, DateTimeOffset asOf, CancellationToken cancellationToken)
        {
            Assert.Equal(ConfigurationFamilyCodes.EvidencePolicy, familyCode);
            return missing is { } reason
                ? Task.FromException<ResolvedConfiguration>(new ConfigurationMissingException(familyCode, reason, null))
                : Task.FromResult(new ResolvedConfiguration(VersionId, familyCode, 1, asOf.AddDays(-1), null, content!));
        }

        public Task<ResolvedConfiguration> ResolvePinnedAsync(Guid versionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
