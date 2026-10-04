using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// The financial-status thresholds (PTBC-025), WORKFLOW_POLICY values resolved as of the moment they are used (E-U2). Nothing has
/// a default: publication needs them and answers 422 CONFIGURATION_MISSING without them; the live position is UNKNOWN instead.
/// </summary>
internal sealed class FinancialPolicy(IConfigurationResolver resolver)
{
    /// <summary>PERCENT: the forecast's overrun of the Approved Budget, in percent of it, from which a position is AMBER.</summary>
    public const string AmberOverrunPercent = "FINANCIAL_STATUS_AMBER_OVERRUN_PERCENT";

    /// <summary>PERCENT: the overrun from which a position is RED; not below the AMBER one.</summary>
    public const string RedOverrunPercent = "FINANCIAL_STATUS_RED_OVERRUN_PERCENT";

    /// <exception cref="ConfigurationMissingException">No WORKFLOW_POLICY in force, or the values are missing or incoherent.</exception>
    public async Task<FinancialStatusRuleVersion> ThresholdsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        ResolvedConfiguration policy = await resolver.ResolveAsync(ConfigurationFamilyCodes.WorkflowPolicy, asOf, cancellationToken).ConfigureAwait(false);
        decimal amber = policy.RequirePercent(AmberOverrunPercent);
        decimal red = policy.RequirePercent(RedOverrunPercent);
        return amber > 0 && red >= amber
            ? new FinancialStatusRuleVersion(policy.VersionId, new FinancialThresholds(amber, red))
            : throw new ConfigurationMissingException(ConfigurationFamilyCodes.WorkflowPolicy, ConfigurationMissingReason.EntryInvalid, $"value {RedOverrunPercent}");
    }

    /// <summary>As <see cref="ThresholdsAsync"/>, or null where it would throw: for a read, which reports UNKNOWN rather than refusing.</summary>
    public async Task<FinancialStatusRuleVersion?> FindThresholdsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        try
        {
            return await ThresholdsAsync(asOf, cancellationToken).ConfigureAwait(false);
        }
        catch (ConfigurationMissingException)
        {
            return null;
        }
    }
}

/// <summary>The thresholds in force and the WORKFLOW_POLICY version they came from, which a snapshot pins (ERD D-13).</summary>
internal sealed record FinancialStatusRuleVersion(Guid ConfigurationVersionId, FinancialThresholds Thresholds);
