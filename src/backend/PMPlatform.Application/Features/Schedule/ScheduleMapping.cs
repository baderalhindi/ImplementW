using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

internal static class ScheduleMapping
{
    public static ProjectScheduleDetail ToDetail(ProjectSchedule s, Guid? activeBaselineId) =>
        new(s.Id, s.ProjectId, s.CalendarItemId, activeBaselineId, s.CreatedAt, s.CreatedBy, s.UpdatedAt, s.UpdatedBy);

    /// <summary>The activity with the ACTIVE baseline's copy of it, if the baseline has one, and the forecast's variance from that copy.</summary>
    public static ScheduleActivityDetail ToDetail(ScheduleActivity a, Guid projectId, ActiveBaseline? baseline)
    {
        BaselineActivity? reference = baseline?.Activities.GetValueOrDefault(a.Id);
        ActivityVariance variance = ScheduleVariance.Of(a, reference);
        return new ScheduleActivityDetail(
            a.Id, projectId, a.ProjectScheduleId, a.ParentActivityId, a.WbsCode, a.Name, a.ActivityKind, a.Status, a.SortOrder, a.RequestedStartDate,
            a.PlannedStartDate, a.PlannedFinishDate, a.PlannedDurationDays, a.ForecastStartDate, a.ForecastFinishDate, a.ActualStartDate, a.ActualFinishDate,
            reference is null ? null : baseline!.Baseline.Id, reference?.PlannedStartDate, reference?.PlannedFinishDate,
            variance.StartVarianceDays, variance.FinishVarianceDays, a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy);
    }

    public static ScheduleDependencyDetail ToDetail(ScheduleDependency d, Guid projectId) =>
        new(d.Id, projectId, d.PredecessorActivityId, d.SuccessorActivityId, d.DependencyType, d.LagDays, d.CreatedAt, d.CreatedBy);

    public static ProjectBaselineDetail ToDetail(ProjectBaseline b) => new(
        b.Id, b.ProjectId, b.BaselineType, b.VersionNo, b.RevisionNo, b.Status, b.BaselineFinishDate, b.ActivatedAt, b.SupersededAt, b.SupersededByBaselineId,
        b.ChangeAuthorizationId, b.ProjectIntakeId, b.DeclaredEndDate, b.DeclaredScope, b.CreatedAt, b.CreatedBy, b.UpdatedAt, b.UpdatedBy);

    public static BaselineActivityDetail ToDetail(BaselineActivity a) =>
        new(a.ScheduleActivityId, a.ParentActivityId, a.ActivityKind, a.PlannedStartDate, a.PlannedFinishDate, a.PlannedDurationDays);

    public static BaselineDependencyDetail ToDetail(BaselineDependency d) => new(d.PredecessorActivityId, d.SuccessorActivityId, d.DependencyType, d.LagDays);

    /// <summary>The milestone with the ACTIVE baseline's date for it, if the baseline has one, and the forecast's variance from that date.</summary>
    public static ProjectMilestoneDetail ToDetail(ProjectMilestone m, ActiveMilestoneBaseline? baseline)
    {
        BaselineMilestone? reference = baseline?.Milestones.GetValueOrDefault(m.Id);
        return new ProjectMilestoneDetail(
            m.Id, m.ProjectId, m.ProjectScheduleId, m.ScheduleActivityId, m.Title, m.MilestoneCategoryItemId, m.ForecastDate, m.Status, m.SortOrder,
            reference is null ? null : baseline!.BaselineId, reference?.PlannedDate, ScheduleVariance.Of(m, reference),
            m.CreatedAt, m.CreatedBy, m.UpdatedAt, m.UpdatedBy);
    }

    public static BaselineMilestoneDetail ToDetail(BaselineMilestone m) => new(m.ProjectMilestoneId, m.PlannedDate);

    public static ScheduleHealthStatusDetail ToDetail(ScheduleHealthStatus h) =>
        new(h.Id, h.ProjectId, h.ProjectBaselineId, h.ScheduleHealth, h.FinishVarianceDays, h.ComputedAt, h.HealthRuleConfigurationVersionId);
}

/// <summary>The ACTIVE baseline's id and its frozen milestone dates by milestone id: the reference of a milestone's variance.</summary>
internal sealed record ActiveMilestoneBaseline(Guid BaselineId, IReadOnlyDictionary<Guid, BaselineMilestone> Milestones);

/// <summary>The ACTIVE baseline and its frozen activities by schedule activity id: the reference of every variance.</summary>
internal sealed record ActiveBaseline(ProjectBaseline Baseline, IReadOnlyDictionary<Guid, BaselineActivity> Activities);
