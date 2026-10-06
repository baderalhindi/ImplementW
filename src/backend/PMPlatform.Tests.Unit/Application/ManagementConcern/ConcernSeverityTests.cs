using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Tests.Unit.Application.ManagementConcern;

/// <summary>
/// TASK-057's first acceptance criterion as rules: severity is computed by the server from the impacts on the shared scale (ADR-011),
/// the overall impact is the highest dimension level, and the severity is the CONCERN_SEVERITY item the RISK_MATRIX version in force
/// names for that level — generically (OQ-006), and failing closed.
/// </summary>
public sealed class ConcernSeverityTests
{
    private static readonly Guid Cost = Guid.NewGuid();
    private static readonly Guid Schedule = Guid.NewGuid();
    private static readonly Guid Reputation = Guid.NewGuid();
    private static readonly Guid Minor = Guid.NewGuid();
    private static readonly Guid Major = Guid.NewGuid();
    private static readonly Guid Critical = Guid.NewGuid();

    /// <summary>A concern assesses the dimensions that apply to it (VAL-ISS-006): any of the scale's, each at a level the scale defines.</summary>
    [Fact]
    public void ImpactsOnSomeOfTheScalesDimensionsFit() =>
        Assert.Empty(ConcernSeverity.Check(Scale(), [Impact(Schedule, 4)]));

    [Fact]
    public void EveryWayImpactsMissTheScaleIsReported()
    {
        Guid unknown = Guid.NewGuid();

        Assert.Equal(
            [
                new FieldIssue("impacts[1].impactDimensionItemId", FieldIssue.Duplicate),
                new FieldIssue("impacts[2].impactDimensionItemId", FieldIssue.NotAllowed),
                new FieldIssue("impacts[3].impactLevel", FieldIssue.NotAllowed),
            ],
            ConcernSeverity.Check(Scale(levels: 4), [Impact(Cost, 2), Impact(Cost, 3), Impact(unknown, 1), Impact(Schedule, 5)]));
    }

    /// <summary>BR-ISS-008: the highest dimension decides, so a severe dimension is never averaged away by mild ones.</summary>
    [Fact]
    public void TheOverallImpactIsTheHighestDimensionLevel()
    {
        Assert.Equal(5, ConcernSeverity.OverallImpactOf([Impact(Cost, 1), Impact(Schedule, 5), Impact(Reputation, 1)]));
        Assert.Equal(2, ConcernSeverity.OverallImpactOf([Impact(Cost, 2), Impact(Reputation, 1)]));
    }

    /// <summary>
    /// BR-ISS-009: the severity is the item the version in force maps the overall level to, pinned with that version's id; the same
    /// impacts under another version's mapping give that version's severity.
    /// </summary>
    [Fact]
    public async Task TheSeverityIsTheItemTheVersionInForceNamesForTheOverallLevel()
    {
        Guid strict = Guid.NewGuid();
        Guid lenient = Guid.NewGuid();
        IReadOnlyList<ConcernImpactInput> impacts = [Impact(Cost, 2), Impact(Schedule, 4)];

        ComputedSeverity underStrict = Succeeded(await Severity(strict, Mapping(4, "TEST_CRITICAL")).ComputeAsync(impacts, DateTimeOffset.UtcNow, CancellationToken.None));
        ComputedSeverity underLenient = Succeeded(await Severity(lenient, Mapping(4, "TEST_MAJOR")).ComputeAsync(impacts, DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Equal(new ComputedSeverity(4, Critical, "TEST_CRITICAL", strict), underStrict);
        Assert.Equal(new ComputedSeverity(4, Major, "TEST_MAJOR", lenient), underLenient);
    }

    [Fact]
    public async Task ImpactsThatMissTheScaleAreRefusedWithEveryMiss()
    {
        AdministrationResult<ComputedSeverity> refused = await Severity(Guid.NewGuid(), Mapping(1, "TEST_MINOR"))
            .ComputeAsync([Impact(Guid.NewGuid(), 1)], DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(refused.Succeeded);
        Assert.Equal(ConcernErrorCodes.ImpactInvalid, refused.Error.Code);
        Assert.Equal([new FieldIssue("impacts[0].impactDimensionItemId", FieldIssue.NotAllowed)], refused.Error.Fields);
    }

    /// <summary>No mapping for the level, or a mapping to an item that is not a PUBLISHED CONCERN_SEVERITY: no severity is guessed.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("TEST_RETIRED")]
    public async Task AMissingOrUnpublishedSeverityFailsClosed(string? mappedCode)
    {
        ConcernSeverity severity = Severity(Guid.NewGuid(), mappedCode is null ? [] : Mapping(3, mappedCode));

        await Assert.ThrowsAsync<ConfigurationMissingException>(() => severity.ComputeAsync([Impact(Cost, 3)], DateTimeOffset.UtcNow, CancellationToken.None));
    }

    private static ConcernImpactInput Impact(Guid dimension, short level) => new(dimension, level, null);

    private static ComputedSeverity Succeeded(AdministrationResult<ComputedSeverity> result)
    {
        Assert.True(result.Succeeded, result.Error?.Code);
        return result.Value;
    }

    /// <summary>The value the version names the CONCERN_SEVERITY code of an overall level by.</summary>
    private static ConfigurationValueEntry[] Mapping(short level, string code) => [new($"{ConcernSeverity.LevelKeyPrefix}{level}", ConfigurationValueType.Text, code)];

    private static ConcernSeverity Severity(Guid versionId, IReadOnlyList<ConfigurationValueEntry> values) =>
        new(new StubResolver(versionId, Scale() with { Values = values }), new StubMasterData());

    /// <summary>The three dimensions at <paramref name="levels"/> levels each.</summary>
    private static ConfigurationContent Scale(int levels = 5)
    {
        BilingualLabel label = new("مستوى", "Level");
        return new ConfigurationContent
        {
            ImpactLevels =
            [
                .. new[] { Cost, Schedule, Reputation }.SelectMany(d => Enumerable.Range(1, levels).Select(l => new ImpactLevelEntry(d, (short)l, label, null, null, null))),
            ],
        };
    }

    private sealed class StubResolver(Guid versionId, ConfigurationContent content) : IConfigurationResolver
    {
        public Task<ResolvedConfiguration> ResolveAsync(string familyCode, DateTimeOffset asOf, CancellationToken cancellationToken) =>
            Task.FromResult(new ResolvedConfiguration(versionId, familyCode, 1, asOf.AddDays(-1), null, content));

        public Task<ResolvedConfiguration> ResolvePinnedAsync(Guid versionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<RiskRatingReference>> ListRiskRatingsAsync(Guid versionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    /// <summary>CONCERN_SEVERITY with three PUBLISHED items; TEST_RETIRED is not among them.</summary>
    private sealed class StubMasterData : IMasterDataResolver
    {
        private static readonly BilingualLabel Label = new("خطورة", "Severity");

        public Task<IReadOnlyList<MasterDataItemReference>> ListPublishedItemsAsync(string catalogueCode, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MasterDataItemReference>>(
            [
                new(Minor, catalogueCode, "TEST_MINOR", Label, null, 1),
                new(Major, catalogueCode, "TEST_MAJOR", Label, null, 2),
                new(Critical, catalogueCode, "TEST_CRITICAL", Label, null, 3),
            ]);

        public Task<MasterDataItemReference> RequirePublishedItemAsync(string catalogueCode, Guid itemId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
