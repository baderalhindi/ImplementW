namespace PMPlatform.Domain.Closure;

/// <summary>ERD <c>readiness_check.result</c>: an evaluation passes or fails; a waiver of a failed check is WAIVED.</summary>
public enum ReadinessResult
{
    Pass = 1,
    Fail = 2,
    Waived = 3,
}
