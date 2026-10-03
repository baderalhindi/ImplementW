using PMPlatform.Domain.Common;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// A new activity. A leaf is planned from <see cref="RequestedStartDate"/> and <see cref="PlannedDurationDays"/> working
/// days; the planned dates are calculated, never sent (the spec's DCL-SCH-03).
/// </summary>
public sealed record ScheduleActivityDraft(
    Guid ProjectId, Guid? ParentActivityId, string WbsCode, NarrativeText Name, DateOnly RequestedStartDate, int PlannedDurationDays, int SortOrder);

/// <summary>An activity's editable inputs, as a whole (R-5).</summary>
public sealed record ScheduleActivityChanges(
    Guid? ParentActivityId, string WbsCode, NarrativeText Name, DateOnly RequestedStartDate, int PlannedDurationDays, int SortOrder);

/// <summary>A leaf's Current Forecast; the finish is not before the start.</summary>
public sealed record ScheduleForecast(DateOnly ForecastStartDate, DateOnly ForecastFinishDate);

public sealed record ScheduleDependencyDraft(Guid PredecessorActivityId, Guid SuccessorActivityId, ScheduleDependencyType DependencyType, int LagDays);

/// <summary>
/// A new milestone of the project's schedule (TASK-050): its title, its MILESTONE_CATEGORY item, its Current Forecast date,
/// and the activity it completes, if any.
/// </summary>
public sealed record ProjectMilestoneDraft(
    Guid ProjectId, Guid? ScheduleActivityId, NarrativeText Title, Guid MilestoneCategoryItemId, DateOnly ForecastDate, int SortOrder);

/// <summary>A milestone's editable inputs, as a whole (R-5).</summary>
public sealed record ProjectMilestoneChanges(Guid? ScheduleActivityId, NarrativeText Title, Guid MilestoneCategoryItemId, DateOnly ForecastDate, int SortOrder);

/// <summary>Submitting a candidate; a rebaseline names the WF-08 change authorisation it implements (BR-SCH-034).</summary>
public sealed record BaselineSubmission(Guid? ChangeAuthorizationId);
