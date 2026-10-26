namespace PMPlatform.Application.Features.Progress.Contracts.Events;

/// <summary>
/// The audit event types Progress produces (TASK-044; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class ProgressAuditEvents
{
    /// <summary>DATA_CHANGE: a reporting period was generated.</summary>
    public const string ReportingCycleCreated = "Progress.ReportingCycleCreated";

    /// <summary>DATA_CHANGE: a period's update was started as a DRAFT, or a returned revision reopened as the next.</summary>
    public const string ProgressUpdateStarted = "Progress.ProgressUpdateStarted";

    /// <summary>DATA_CHANGE: the narrative or the override of a DRAFT.</summary>
    public const string ProgressUpdateChanged = "Progress.ProgressUpdateChanged";

    /// <summary>DATA_CHANGE: the ADR-014 opening position was recorded, submitted for review.</summary>
    public const string OpeningPositionRecorded = "Progress.OpeningPositionRecorded";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT → SUBMITTED.</summary>
    public const string ProgressSubmitted = "Progress.ProgressSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → UNDER_REVIEW.</summary>
    public const string ReviewStarted = "Progress.ReviewStarted";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → RETURNED.</summary>
    public const string ProgressReturned = "Progress.ProgressReturned";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → PUBLISHED; the snapshot was written and the period closed.</summary>
    public const string ProgressPublished = "Progress.ProgressPublished";

    /// <summary>DATA_CHANGE: the CURRENT/LIVE Overall Project Health was recomputed.</summary>
    public const string ProjectHealthRecomputed = "Progress.ProjectHealthRecomputed";

    /// <summary>AUTHORIZATION_DENIAL: an external user, or the submitter, asked to review a submission.</summary>
    public const string ReviewRefused = "Progress.ReviewRefused";
}
