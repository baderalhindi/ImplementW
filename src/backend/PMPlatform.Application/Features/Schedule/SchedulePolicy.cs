using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>The configuration WF-03 reads, resolved as of the moment it is used (E-U2).</summary>
internal sealed class SchedulePolicy(IConfigurationResolver resolver)
{
    /// <summary>DURATION_DAYS in WORKFLOW_POLICY: working days late at finish from which the schedule is AMBER (TBC-SCH-003).</summary>
    public const string AmberFinishVarianceDays = "SCHEDULE_HEALTH_AMBER_FINISH_VARIANCE_DAYS";

    /// <summary>DURATION_DAYS in WORKFLOW_POLICY: working days late at finish from which the schedule is RED; not below the AMBER one.</summary>
    public const string RedFinishVarianceDays = "SCHEDULE_HEALTH_RED_FINISH_VARIANCE_DAYS";

    /// <summary>
    /// The health thresholds in force at <paramref name="asOf"/>, with the version a row pins (ERD D-13); null when they are
    /// missing or incoherent. Unlike WF-02's, a missing threshold does not refuse the command: Schedule Health is a side effect
    /// of every schedule write and of WF-11's outcome, which must not fail for it, so the health is UNKNOWN instead
    /// (schedule-baseline.md D-9).
    /// </summary>
    public async Task<ScheduleHealthRuleVersion?> HealthRuleAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        try
        {
            ResolvedConfiguration policy = await resolver.ResolveAsync(ConfigurationFamilyCodes.WorkflowPolicy, asOf, cancellationToken).ConfigureAwait(false);
            int amber = policy.RequireDurationDays(AmberFinishVarianceDays);
            int red = policy.RequireDurationDays(RedFinishVarianceDays);
            return amber > 0 && red >= amber ? new ScheduleHealthRuleVersion(policy.VersionId, new ScheduleHealthThresholds(amber, red)) : null;
        }
        catch (ConfigurationMissingException)
        {
            return null;
        }
    }

    /// <summary>ADR-015: whether a baseline of the profile goes through WF-11 (Standard and Full) or activates on submission (Light).</summary>
    /// <exception cref="ConfigurationMissingException">The profile has no published settings.</exception>
    public async Task<bool> RequiresBaselineApprovalAsync(Guid governanceProfileItemId, DateTimeOffset asOf, CancellationToken cancellationToken) =>
        (await resolver.ResolveAsync(ConfigurationFamilyCodes.GovernanceProfile, asOf, cancellationToken).ConfigureAwait(false))
            .RequireGovernanceProfile(governanceProfileItemId).RequiresBaselineApproval;
}

internal sealed record ScheduleHealthRuleVersion(Guid ConfigurationVersionId, ScheduleHealthThresholds Thresholds);
