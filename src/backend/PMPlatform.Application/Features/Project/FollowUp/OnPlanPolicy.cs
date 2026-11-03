using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Project.FollowUp;

/// <summary>
/// The on-plan thresholds and the entity compliance share (the 2 Nov 2026 review §2), WORKFLOW_POLICY values resolved as of the moment they are used. Unlike WF-02's health,
/// the indicator has defaults — RPMO's own −5 and −10 points — so the labels RPMO uses today show before AHDA publishes a policy. A policy that
/// sets them incoherently (stalled before late) is not used.
/// </summary>
internal sealed class OnPlanPolicy(IConfigurationResolver resolver)
{
    /// <summary>PERCENT in WORKFLOW_POLICY: points behind plan from which a project is late.</summary>
    public const string LatePoints = "ON_PLAN_LATE_GAP_POINTS";

    /// <summary>PERCENT in WORKFLOW_POLICY: points behind plan from which a project is stalled; not below the late one.</summary>
    public const string StalledPoints = "ON_PLAN_STALLED_GAP_POINTS";

    /// <summary>PERCENT in WORKFLOW_POLICY: the share of update requests answered on time from which an entity keeps to its updates.</summary>
    public const string CompliancePercent = "ENTITY_UPDATE_COMPLIANCE_PERCENT";

    public async Task<OnPlanThresholds> ThresholdsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        try
        {
            ResolvedConfiguration policy = await resolver.ResolveAsync(ConfigurationFamilyCodes.WorkflowPolicy, asOf, cancellationToken).ConfigureAwait(false);
            decimal late = Optional(policy, LatePoints) ?? OnPlanThresholds.Default.LatePoints;
            decimal stalled = Optional(policy, StalledPoints) ?? OnPlanThresholds.Default.StalledPoints;
            decimal compliance = Optional(policy, CompliancePercent) ?? OnPlanThresholds.Default.CompliancePercent;
            return late > 0 && stalled >= late && compliance is > 0 and <= 100
                ? new OnPlanThresholds(late, stalled, compliance)
                : OnPlanThresholds.Default;
        }
        catch (ConfigurationMissingException)
        {
            return OnPlanThresholds.Default;
        }
    }

    /// <summary>A value the policy may leave out: the default holds then.</summary>
    private static decimal? Optional(ResolvedConfiguration policy, string key)
    {
        try
        {
            return policy.RequirePercent(key);
        }
        catch (ConfigurationMissingException)
        {
            return null;
        }
    }
}
