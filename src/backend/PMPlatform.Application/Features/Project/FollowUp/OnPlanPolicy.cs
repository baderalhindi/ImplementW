using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Project.FollowUp;

/// <summary>
/// The on-plan thresholds (the 2 Nov 2026 review §2), WORKFLOW_POLICY values resolved as of the moment they are used. Unlike WF-02's health,
/// the indicator has defaults — RPMO's own −5 and −10 points — so the labels RPMO uses today show before AHDA publishes a policy. A policy that
/// sets them incoherently (stalled before late) is not used.
/// </summary>
internal sealed class OnPlanPolicy(IConfigurationResolver resolver)
{
    /// <summary>PERCENT in WORKFLOW_POLICY: points behind plan from which a project is late.</summary>
    public const string LatePoints = "ON_PLAN_LATE_GAP_POINTS";

    /// <summary>PERCENT in WORKFLOW_POLICY: points behind plan from which a project is stalled; not below the late one.</summary>
    public const string StalledPoints = "ON_PLAN_STALLED_GAP_POINTS";

    public async Task<OnPlanThresholds> ThresholdsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        try
        {
            ResolvedConfiguration policy = await resolver.ResolveAsync(ConfigurationFamilyCodes.WorkflowPolicy, asOf, cancellationToken).ConfigureAwait(false);
            decimal late = policy.RequirePercent(LatePoints);
            decimal stalled = policy.RequirePercent(StalledPoints);
            return late > 0 && stalled >= late ? new OnPlanThresholds(late, stalled) : OnPlanThresholds.Default;
        }
        catch (ConfigurationMissingException)
        {
            return OnPlanThresholds.Default;
        }
    }
}
