using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>What WF-04 needs to know about a schedule activity (edge 9): its project, whether it is a live leaf, and its planned dates.</summary>
public sealed record ScheduleActivityFacts(
    Guid Id, Guid ProjectId, ScheduleActivityKind ActivityKind, ScheduleActivityStatus Status, DateOnly PlannedStartDate, DateOnly PlannedFinishDate, int PlannedDurationDays);
