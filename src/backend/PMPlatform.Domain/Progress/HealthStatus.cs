namespace PMPlatform.Domain.Progress;

/// <summary>
/// A health rating. UNKNOWN is a value of its own: a rating whose inputs are missing is UNKNOWN, never coerced to a
/// colour (ERD <c>published_progress_snapshot.overall_health</c>).
/// </summary>
public enum HealthStatus
{
    Green = 1,
    Amber = 2,
    Red = 3,
    Unknown = 4,
}
