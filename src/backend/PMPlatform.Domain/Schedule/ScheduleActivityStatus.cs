namespace PMPlatform.Domain.Schedule;

/// <summary>
/// ERD <c>schedule_activity.status</c>. WF-03 writes PLANNED and CANCELLED; IN_PROGRESS and COMPLETED are execution
/// states WF-04 reports (the spec's DCL-SCH-18). CANCELLED is final.
/// </summary>
public enum ScheduleActivityStatus
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
}
