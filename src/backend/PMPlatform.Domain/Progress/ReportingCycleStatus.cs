namespace PMPlatform.Domain.Progress;

/// <summary>A reporting period is OPEN until its progress is published, then CLOSED for good (TASK-044).</summary>
public enum ReportingCycleStatus
{
    Open = 1,
    Closed = 2,
}
