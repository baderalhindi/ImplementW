using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// The configuration WF-02 reads, resolved as of the moment it is used (E-U2). Nothing has a default: without a cadence no
/// period can be generated, and without thresholds no health can be computed, so each operation that needs one answers
/// 422 CONFIGURATION_MISSING (Blueprint Section 12).
/// </summary>
internal sealed class ProgressPolicy(IConfigurationResolver resolver)
{
    /// <summary>PERCENT in WORKFLOW_POLICY: the slippage of actual behind planned, in percentage points, from which progress is AMBER.</summary>
    public const string AmberSlippagePercent = "PROGRESS_HEALTH_AMBER_SLIPPAGE_PERCENT";

    /// <summary>PERCENT in WORKFLOW_POLICY: the slippage from which progress is RED; not below the AMBER one.</summary>
    public const string RedSlippagePercent = "PROGRESS_HEALTH_RED_SLIPPAGE_PERCENT";

    /// <summary>The health rule in force at <paramref name="asOf"/>, with the version a record pins (ERD D-13).</summary>
    public async Task<HealthRule> HealthRuleAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        ResolvedConfiguration policy = await resolver.ResolveAsync(ConfigurationFamilyCodes.WorkflowPolicy, asOf, cancellationToken).ConfigureAwait(false);
        decimal amber = policy.RequirePercent(AmberSlippagePercent);
        decimal red = policy.RequirePercent(RedSlippagePercent);
        return amber > 0 && red >= amber
            ? new HealthRule(policy.VersionId, new HealthThresholds(amber, red))
            : throw new ConfigurationMissingException(ConfigurationFamilyCodes.WorkflowPolicy, ConfigurationMissingReason.EntryInvalid, $"value {RedSlippagePercent}");
    }

    /// <summary>ADR-015: how many days a reporting period of the profile lasts.</summary>
    public async Task<int> UpdateCadenceDaysAsync(Guid governanceProfileItemId, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        int days = (await resolver.ResolveAsync(ConfigurationFamilyCodes.GovernanceProfile, asOf, cancellationToken).ConfigureAwait(false))
            .RequireGovernanceProfile(governanceProfileItemId).UpdateCadenceDays;
        return days > 0
            ? days
            : throw new ConfigurationMissingException(ConfigurationFamilyCodes.GovernanceProfile, ConfigurationMissingReason.EntryInvalid, $"update cadence of {governanceProfileItemId}");
    }
}

internal sealed record HealthRule(Guid ConfigurationVersionId, HealthThresholds Thresholds);
