using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>The audit events of WF-03 (TASK-033 <c>IAuditTrail</c>). Names and scopes are free text and are not copied.</summary>
internal static class ScheduleAudit
{
    public const string Module = "Schedule";
    public const string ScheduleType = "ProjectSchedule";
    public const string ActivityType = "ScheduleActivity";
    public const string DependencyType = "ScheduleDependency";
    public const string BaselineType = "ProjectBaseline";
    public const string HealthType = "ScheduleHealthStatus";
    public const string MilestoneType = "ProjectMilestone";

    /// <summary>Why an approved candidate was returned instead of activated.</summary>
    public const string RebaselineNotAuthorized = "REBASELINE_NOT_AUTHORIZED";

    public static AuditEntry Initialized(Guid actorId, ProjectFacts project, ProjectSchedule schedule) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.ScheduleInitialized, actorId, project, new AuditSubject(Module, ScheduleType, schedule.Id), []);

    public static AuditEntry ActivityCreated(Guid actorId, ProjectFacts project, ScheduleActivity activity, int recalculated) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.ActivityCreated, actorId, project, SubjectOf(activity),
        [
            AuditAttribute.Of(ScheduleAuditAttributes.ProjectScheduleId, activity.ProjectScheduleId),
            AuditAttribute.Of(ScheduleAuditAttributes.ParentActivityId, activity.ParentActivityId),
            AuditAttribute.Of(ScheduleAuditAttributes.WbsCode, activity.WbsCode),
            AuditAttribute.Of(ScheduleAuditAttributes.RequestedStartDate, activity.RequestedStartDate),
            AuditAttribute.Of(ScheduleAuditAttributes.PlannedDurationDays, activity.PlannedDurationDays),
            AuditAttribute.Of(ScheduleAuditAttributes.RecalculatedActivityCount, recalculated),
        ]);

    public static AuditEntry ActivityChanged(Guid actorId, ProjectFacts project, ActivityInputs before, ScheduleActivity activity, int recalculated) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.ActivityChanged, actorId, project, SubjectOf(activity),
            new[]
            {
                AuditAttribute.Change(ScheduleAuditAttributes.ParentActivityId, before.ParentActivityId, activity.ParentActivityId),
                AuditAttribute.Change(ScheduleAuditAttributes.WbsCode, before.WbsCode, activity.WbsCode),
                AuditAttribute.WithheldChange(ScheduleAuditAttributes.Name, before.Name.Text, activity.Name.Text),
                AuditAttribute.Change(ScheduleAuditAttributes.RequestedStartDate, before.RequestedStartDate, activity.RequestedStartDate),
                AuditAttribute.Change(ScheduleAuditAttributes.PlannedDurationDays, before.PlannedDurationDays, activity.PlannedDurationDays),
                AuditAttribute.Change(ScheduleAuditAttributes.SortOrder, before.SortOrder, activity.SortOrder),
                AuditAttribute.Of(ScheduleAuditAttributes.RecalculatedActivityCount, recalculated),
            }.OfType<AuditAttribute>());

    public static AuditEntry ActivityCancelled(Guid actorId, ProjectFacts project, ScheduleActivity activity, int recalculated) =>
        Entry(AuditEventClass.LifecycleTransition, ScheduleAuditEvents.ActivityCancelled, actorId, project, SubjectOf(activity),
        [
            AuditAttribute.Change(ScheduleAuditAttributes.Status, ScheduleActivityStatus.Planned, activity.Status)!,
            AuditAttribute.Of(ScheduleAuditAttributes.RecalculatedActivityCount, recalculated),
        ]);

    public static AuditEntry ActivityReforecast(Guid actorId, ProjectFacts project, ScheduleForecastDates before, ScheduleActivity activity) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.ActivityReforecast, actorId, project, SubjectOf(activity),
            new[]
            {
                AuditAttribute.Change(ScheduleAuditAttributes.ForecastStartDate, before.Start, activity.ForecastStartDate),
                AuditAttribute.Change(ScheduleAuditAttributes.ForecastFinishDate, before.Finish, activity.ForecastFinishDate),
            }.OfType<AuditAttribute>());

    public static AuditEntry DependencyCreated(Guid actorId, ProjectFacts project, ScheduleDependency dependency, int recalculated) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.DependencyCreated, actorId, project, SubjectOf(dependency),
        [
            .. DependencyAttributes(dependency),
            AuditAttribute.Of(ScheduleAuditAttributes.RecalculatedActivityCount, recalculated),
        ]);

    public static AuditEntry DependencyDeleted(Guid actorId, ProjectFacts project, ScheduleDependency dependency, int recalculated) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.DependencyDeleted, actorId, project, SubjectOf(dependency),
        [
            .. DependencyAttributes(dependency),
            AuditAttribute.Of(ScheduleAuditAttributes.RecalculatedActivityCount, recalculated),
        ]);

    public static AuditEntry MilestoneCreated(Guid actorId, ProjectFacts project, ProjectMilestone milestone) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.MilestoneCreated, actorId, project, SubjectOf(milestone),
        [
            AuditAttribute.Of(ScheduleAuditAttributes.ProjectScheduleId, milestone.ProjectScheduleId),
            AuditAttribute.Of(ScheduleAuditAttributes.ScheduleActivityId, milestone.ScheduleActivityId),
            AuditAttribute.Of(ScheduleAuditAttributes.MilestoneCategoryItemId, milestone.MilestoneCategoryItemId),
            AuditAttribute.Of(ScheduleAuditAttributes.ForecastDate, milestone.ForecastDate),
            AuditAttribute.Of(ScheduleAuditAttributes.SortOrder, milestone.SortOrder),
        ]);

    public static AuditEntry MilestoneChanged(Guid actorId, ProjectFacts project, MilestoneInputs before, ProjectMilestone milestone) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.MilestoneChanged, actorId, project, SubjectOf(milestone),
            new[]
            {
                AuditAttribute.Change(ScheduleAuditAttributes.ScheduleActivityId, before.ScheduleActivityId, milestone.ScheduleActivityId),
                AuditAttribute.WithheldChange(ScheduleAuditAttributes.Title, before.Title.Text, milestone.Title.Text),
                AuditAttribute.Change(ScheduleAuditAttributes.MilestoneCategoryItemId, before.MilestoneCategoryItemId, milestone.MilestoneCategoryItemId),
                AuditAttribute.Change(ScheduleAuditAttributes.ForecastDate, before.ForecastDate, milestone.ForecastDate),
                AuditAttribute.Change(ScheduleAuditAttributes.SortOrder, before.SortOrder, milestone.SortOrder),
            }.OfType<AuditAttribute>());

    public static AuditEntry MilestoneCancelled(Guid actorId, ProjectFacts project, ProjectMilestone milestone) =>
        Entry(AuditEventClass.LifecycleTransition, ScheduleAuditEvents.MilestoneCancelled, actorId, project, SubjectOf(milestone),
            [AuditAttribute.Change(ScheduleAuditAttributes.Status, ProjectMilestoneStatus.Planned, milestone.Status)!]);

    /// <summary>ICD-04: WF-05 accepted the achievement; the accepted date is WF-05's and is not copied here.</summary>
    public static AuditEntry MilestoneAchieved(Guid actorId, ProjectFacts project, ProjectMilestone milestone, Guid milestoneAchievementId) =>
        Entry(AuditEventClass.LifecycleTransition, ScheduleAuditEvents.MilestoneAchieved, actorId, project, SubjectOf(milestone),
        [
            AuditAttribute.Change(ScheduleAuditAttributes.Status, ProjectMilestoneStatus.Planned, milestone.Status)!,
            AuditAttribute.Of(ScheduleAuditAttributes.MilestoneAchievementId, milestoneAchievementId),
        ]);

    public static AuditEntry BaselineCreated(Guid actorId, ProjectFacts project, ProjectBaseline baseline) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.BaselineCreated, actorId, project, SubjectOf(baseline), BaselineAttributes(baseline));

    public static AuditEntry BaselineDeleted(Guid actorId, ProjectFacts project, ProjectBaseline baseline) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.BaselineDeleted, actorId, project, SubjectOf(baseline), BaselineAttributes(baseline));

    public static AuditEntry BaselineSubmitted(Guid actorId, ProjectFacts project, ProjectBaseline baseline, ProjectBaselineStatus from, Guid approvalInstanceId) =>
        Transition(ScheduleAuditEvents.BaselineSubmitted, actorId, project, baseline, from,
        [
            AuditAttribute.Of(ScheduleAuditAttributes.ApprovalInstanceId, approvalInstanceId),
            AuditAttribute.Of(ScheduleAuditAttributes.ChangeAuthorizationId, baseline.ChangeAuthorizationId),
            AuditAttribute.Of(ScheduleAuditAttributes.BaselineFinishDate, baseline.BaselineFinishDate),
        ]);

    public static AuditEntry BaselineActivated(Guid actorId, ProjectFacts project, ProjectBaseline baseline, ProjectBaselineStatus from, ProjectBaseline? superseded, bool approvalRequired) =>
        Transition(ScheduleAuditEvents.BaselineActivated, actorId, project, baseline, from,
        [
            AuditAttribute.Of(ScheduleAuditAttributes.BaselineType, baseline.BaselineType),
            AuditAttribute.Of(ScheduleAuditAttributes.VersionNo, baseline.VersionNo),
            AuditAttribute.Of(ScheduleAuditAttributes.ApprovalRequired, approvalRequired),
            AuditAttribute.Of(ScheduleAuditAttributes.SupersededBaselineId, superseded?.Id),
            AuditAttribute.Of(ScheduleAuditAttributes.ChangeAuthorizationId, baseline.ChangeAuthorizationId),
            AuditAttribute.Of(ScheduleAuditAttributes.BaselineFinishDate, baseline.BaselineFinishDate),
        ]);

    public static AuditEntry BaselineSuperseded(Guid actorId, ProjectFacts project, ProjectBaseline baseline) =>
        Transition(ScheduleAuditEvents.BaselineSuperseded, actorId, project, baseline, ProjectBaselineStatus.Active,
            [AuditAttribute.Of(ScheduleAuditAttributes.SupersededByBaselineId, baseline.SupersededByBaselineId)]);

    /// <summary>WF-11's outcome applied: RETURNED, REJECTED or WITHDRAWN, or RETURNED because the approved candidate can no longer activate.</summary>
    public static AuditEntry OutcomeApplied(string eventType, ProjectFacts project, ProjectBaseline baseline, ApprovalOutcomeRecorded outcome, string? reason) =>
        Transition(eventType, outcome.Data.DecidedByUserId, project, baseline, ProjectBaselineStatus.Submitted,
        [
            AuditAttribute.Of(ScheduleAuditAttributes.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
            AuditAttribute.Of(ScheduleAuditAttributes.Decision, outcome.Data.Decision),
            AuditAttribute.Of(ScheduleAuditAttributes.Reason, reason),
        ]);

    public static AuditEntry OutcomeIgnored(ProjectFacts project, ProjectBaseline baseline, ApprovalOutcomeRecorded outcome) =>
        new(AuditEventClass.LifecycleTransition, ScheduleAuditEvents.ApprovalOutcomeIgnored, AuditOutcome.Failed)
        {
            ActorUserId = outcome.Data.DecidedByUserId,
            Subject = SubjectOf(baseline),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes =
            [
                AuditAttribute.Of(ScheduleAuditAttributes.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
                AuditAttribute.Of(ScheduleAuditAttributes.Decision, outcome.Data.Decision),
                AuditAttribute.Of(ScheduleAuditAttributes.SubjectRevisionNo, outcome.Subject.RevisionNo),
                AuditAttribute.Of(ScheduleAuditAttributes.Status, baseline.Status),
                AuditAttribute.Of(ScheduleAuditAttributes.RevisionNo, baseline.RevisionNo),
            ],
        };

    public static AuditEntry DeclaredBaselineRecorded(Guid actorId, ProjectFacts project, ProjectBaseline baseline) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.DeclaredBaselineRecorded, actorId, project, SubjectOf(baseline),
        [
            .. BaselineAttributes(baseline),
            AuditAttribute.Of(ScheduleAuditAttributes.Status, baseline.Status),
            AuditAttribute.Of(ScheduleAuditAttributes.ProjectIntakeId, baseline.ProjectIntakeId),
            AuditAttribute.Of(ScheduleAuditAttributes.DeclaredEndDate, baseline.DeclaredEndDate),
        ]);

    public static AuditEntry HealthRecomputed(Guid actorId, ProjectFacts project, ScheduleHealthStatus health, ScheduleHealth? before, int? varianceBefore) =>
        Entry(AuditEventClass.DataChange, ScheduleAuditEvents.ScheduleHealthRecomputed, actorId, project, new AuditSubject(Module, HealthType, health.Id),
        [
            AuditAttribute.Change(ScheduleAuditAttributes.ScheduleHealth, before, health.ScheduleHealth) ?? AuditAttribute.Of(ScheduleAuditAttributes.ScheduleHealth, health.ScheduleHealth),
            AuditAttribute.Change(ScheduleAuditAttributes.FinishVarianceDays, varianceBefore, health.FinishVarianceDays)
                ?? AuditAttribute.Of(ScheduleAuditAttributes.FinishVarianceDays, health.FinishVarianceDays),
            AuditAttribute.Of(ScheduleAuditAttributes.ProjectBaselineId, health.ProjectBaselineId),
            AuditAttribute.Of(ScheduleAuditAttributes.HealthRuleConfigurationVersionId, health.HealthRuleConfigurationVersionId),
        ]);

    private static AuditAttribute[] DependencyAttributes(ScheduleDependency dependency) =>
    [
        AuditAttribute.Of(ScheduleAuditAttributes.PredecessorActivityId, dependency.PredecessorActivityId),
        AuditAttribute.Of(ScheduleAuditAttributes.SuccessorActivityId, dependency.SuccessorActivityId),
        AuditAttribute.Of(ScheduleAuditAttributes.DependencyType, dependency.DependencyType),
        AuditAttribute.Of(ScheduleAuditAttributes.LagDays, dependency.LagDays),
    ];

    private static AuditAttribute[] BaselineAttributes(ProjectBaseline baseline) =>
    [
        AuditAttribute.Of(ScheduleAuditAttributes.BaselineType, baseline.BaselineType),
        AuditAttribute.Of(ScheduleAuditAttributes.VersionNo, baseline.VersionNo),
        AuditAttribute.Of(ScheduleAuditAttributes.BaselineFinishDate, baseline.BaselineFinishDate),
    ];

    private static AuditEntry Transition(
        string eventType, Guid actorId, ProjectFacts project, ProjectBaseline baseline, ProjectBaselineStatus from, IEnumerable<AuditAttribute> attributes) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, SubjectOf(baseline),
        [
            AuditAttribute.Change(ScheduleAuditAttributes.Status, from, baseline.Status)!,
            AuditAttribute.Of(ScheduleAuditAttributes.RevisionNo, baseline.RevisionNo),
            .. attributes,
        ]);

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, ProjectFacts project, AuditSubject subject, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = subject,
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes],
        };

    private static AuditSubject SubjectOf(ScheduleActivity activity) => new(Module, ActivityType, activity.Id);

    private static AuditSubject SubjectOf(ScheduleDependency dependency) => new(Module, DependencyType, dependency.Id);

    private static AuditSubject SubjectOf(ProjectBaseline baseline) => new(Module, BaselineType, baseline.Id);

    private static AuditSubject SubjectOf(ProjectMilestone milestone) => new(Module, MilestoneType, milestone.Id);
}

/// <summary>A milestone's inputs before a change, for its audit event.</summary>
internal sealed record MilestoneInputs(Guid? ScheduleActivityId, NarrativeText Title, Guid MilestoneCategoryItemId, DateOnly ForecastDate, int SortOrder)
{
    public static MilestoneInputs Of(ProjectMilestone m) => new(m.ScheduleActivityId, m.Title, m.MilestoneCategoryItemId, m.ForecastDate, m.SortOrder);
}

/// <summary>An activity's inputs before a change, for its audit event.</summary>
internal sealed record ActivityInputs(Guid? ParentActivityId, string WbsCode, NarrativeText Name, DateOnly RequestedStartDate, int PlannedDurationDays, int SortOrder)
{
    public static ActivityInputs Of(ScheduleActivity a) => new(a.ParentActivityId, a.WbsCode, a.Name, a.RequestedStartDate, a.PlannedDurationDays, a.SortOrder);
}

/// <summary>A leaf's forecast before a change, for its audit event.</summary>
internal sealed record ScheduleForecastDates(DateOnly Start, DateOnly Finish);
