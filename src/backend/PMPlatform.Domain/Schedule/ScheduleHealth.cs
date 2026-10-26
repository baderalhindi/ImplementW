namespace PMPlatform.Domain.Schedule;

/// <summary>
/// Schedule Health (ERD <c>schedule_health_status.schedule_health</c>): the spec's On Track, At Risk and Delayed are
/// GREEN, AMBER and RED. UNKNOWN is a value of its own, never coerced to a colour.
/// </summary>
public enum ScheduleHealth
{
    Green = 1,
    Amber = 2,
    Red = 3,
    Unknown = 4,
}
