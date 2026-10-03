using PMPlatform.Domain.Common;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>A project's working schedule, with the baseline that is ACTIVE now, if any.</summary>
public sealed record ProjectScheduleDetail(
    Guid Id, Guid ProjectId, Guid? CalendarItemId, Guid? ActiveBaselineId, DateTimeOffset CreatedAt, Guid CreatedBy, DateTimeOffset UpdatedAt, Guid UpdatedBy);

/// <summary>
/// An activity with its three sets of dates side by side (BR-SCH-002): the working plan, the Current Forecast, and the
/// ACTIVE baseline's copy, with the forecast's variance from the baseline in working days, positive = late. The baseline
/// dates and the variance are null when the ACTIVE baseline does not include the activity, or there is none.
/// </summary>
public sealed record ScheduleActivityDetail(
    Guid Id,
    Guid ProjectId,
    Guid ProjectScheduleId,
    Guid? ParentActivityId,
    string WbsCode,
    NarrativeText Name,
    ScheduleActivityKind ActivityKind,
    ScheduleActivityStatus Status,
    int SortOrder,
    DateOnly RequestedStartDate,
    DateOnly PlannedStartDate,
    DateOnly PlannedFinishDate,
    int PlannedDurationDays,
    DateOnly ForecastStartDate,
    DateOnly ForecastFinishDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualFinishDate,
    Guid? BaselineId,
    DateOnly? BaselineStartDate,
    DateOnly? BaselineFinishDate,
    int? StartVarianceDays,
    int? FinishVarianceDays,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record ScheduleDependencyDetail(
    Guid Id, Guid ProjectId, Guid PredecessorActivityId, Guid SuccessorActivityId, ScheduleDependencyType DependencyType, int LagDays,
    DateTimeOffset CreatedAt, Guid CreatedBy);

/// <summary>A baseline of the project, APPROVED or DECLARED (ADR-014), with its place in the project's baseline history.</summary>
public sealed record ProjectBaselineDetail(
    Guid Id,
    Guid ProjectId,
    BaselineType BaselineType,
    int VersionNo,
    int RevisionNo,
    ProjectBaselineStatus Status,
    DateOnly BaselineFinishDate,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? SupersededAt,
    Guid? SupersededByBaselineId,
    Guid? ChangeAuthorizationId,
    Guid? ProjectIntakeId,
    DateOnly? DeclaredEndDate,
    NarrativeText? DeclaredScope,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>An activity as its baseline froze it.</summary>
public sealed record BaselineActivityDetail(
    Guid ScheduleActivityId, Guid? ParentActivityId, ScheduleActivityKind ActivityKind, DateOnly PlannedStartDate, DateOnly PlannedFinishDate, int PlannedDurationDays);

/// <summary>A dependency as its baseline froze it.</summary>
public sealed record BaselineDependencyDetail(Guid PredecessorActivityId, Guid SuccessorActivityId, ScheduleDependencyType DependencyType, int LagDays);

/// <summary>
/// A milestone as WF-03 holds it (ICD-04): its schedule representation and status, with the ACTIVE baseline's planned date and
/// the forecast's variance from it in working days, positive = late; both null when the ACTIVE baseline does not include the
/// milestone, or there is none. Its achievement — evidence and the accepted Actual Achievement Date — is WF-05's:
/// <c>GET /milestone-achievements?projectMilestoneId={id}</c>.
/// </summary>
public sealed record ProjectMilestoneDetail(
    Guid Id,
    Guid ProjectId,
    Guid ProjectScheduleId,
    Guid? ScheduleActivityId,
    NarrativeText Title,
    Guid MilestoneCategoryItemId,
    DateOnly ForecastDate,
    ProjectMilestoneStatus Status,
    int SortOrder,
    Guid? BaselineId,
    DateOnly? BaselinePlannedDate,
    int? ForecastVarianceDays,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>A milestone's date as its baseline froze it.</summary>
public sealed record BaselineMilestoneDetail(Guid ProjectMilestoneId, DateOnly PlannedDate);

/// <summary>The CURRENT/LIVE Schedule Health, as WF-03 last computed it.</summary>
public sealed record ScheduleHealthStatusDetail(
    Guid Id, Guid ProjectId, Guid? ProjectBaselineId, ScheduleHealth ScheduleHealth, int? FinishVarianceDays, DateTimeOffset ComputedAt, Guid? HealthRuleConfigurationVersionId);
