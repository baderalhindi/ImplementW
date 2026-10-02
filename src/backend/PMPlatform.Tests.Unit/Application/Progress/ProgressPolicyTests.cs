using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Progress;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Tests.Unit.Application.Progress;

/// <summary>WF-02's configuration has no defaults: without a usable rule or cadence the operation stops (Blueprint Section 12).</summary>
public sealed class ProgressPolicyTests
{
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly Guid Standard = Guid.NewGuid();

    [Fact]
    public async Task TheHealthRuleIsTheThresholdsWithTheVersionThatHoldsThem()
    {
        HealthRule rule = await Policy(Values(("PROGRESS_HEALTH_AMBER_SLIPPAGE_PERCENT", "5"), ("PROGRESS_HEALTH_RED_SLIPPAGE_PERCENT", "15")))
            .HealthRuleAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(new HealthRule(VersionId, new HealthThresholds(5, 15)), rule);
    }

    [Theory]
    [InlineData(null, "15", ConfigurationMissingReason.EntryMissing)]
    [InlineData("5", null, ConfigurationMissingReason.EntryMissing)]
    [InlineData("20", "15", ConfigurationMissingReason.EntryInvalid)]   // RED before AMBER
    [InlineData("0", "15", ConfigurationMissingReason.EntryInvalid)]    // every slippage AMBER
    public async Task AnAbsentOrIncoherentHealthRuleFailsClosed(string? amber, string? red, ConfigurationMissingReason reason)
    {
        ConfigurationMissingException missing = await Assert.ThrowsAsync<ConfigurationMissingException>(() =>
            Policy(Values(("PROGRESS_HEALTH_AMBER_SLIPPAGE_PERCENT", amber), ("PROGRESS_HEALTH_RED_SLIPPAGE_PERCENT", red)))
                .HealthRuleAsync(DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Equal((ConfigurationFamilyCodes.WorkflowPolicy, reason), (missing.ConfigurationCode, missing.Reason));
    }

    [Fact]
    public async Task TheCadenceIsTheGovernanceProfiles()
    {
        ProgressPolicy policy = Policy(new ConfigurationContent { GovernanceProfiles = [Profile(Standard, 30)] });

        Assert.Equal(30, await policy.UpdateCadenceDaysAsync(Standard, DateTimeOffset.UtcNow, CancellationToken.None));
        await Assert.ThrowsAsync<ConfigurationMissingException>(() => policy.UpdateCadenceDaysAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task ACadenceOfNoDaysFailsClosed() =>
        Assert.Equal(
            ConfigurationMissingReason.EntryInvalid,
            (await Assert.ThrowsAsync<ConfigurationMissingException>(() =>
                Policy(new ConfigurationContent { GovernanceProfiles = [Profile(Standard, 0)] }).UpdateCadenceDaysAsync(Standard, DateTimeOffset.UtcNow, CancellationToken.None))).Reason);

    private static ConfigurationContent Values(params (string Key, string? Value)[] values) => new()
    {
        Values = [.. values.Where(v => v.Value is not null).Select(v => new ConfigurationValueEntry(v.Key, ConfigurationValueType.Percent, v.Value!))],
    };

    private static GovernanceProfileEntry Profile(Guid id, int cadenceDays) => new(id, true, true, 3, cadenceDays, Guid.NewGuid(), true, null, null, []);

    private static ProgressPolicy Policy(ConfigurationContent content) => new(new StubResolver(content));

    private sealed class StubResolver(ConfigurationContent content) : IConfigurationResolver
    {
        public Task<ResolvedConfiguration> ResolveAsync(string familyCode, DateTimeOffset asOf, CancellationToken cancellationToken) =>
            Task.FromResult(new ResolvedConfiguration(VersionId, familyCode, 1, asOf.AddDays(-1), null, content));

        public Task<ResolvedConfiguration> ResolvePinnedAsync(Guid versionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
