namespace PMPlatform.Application.Features.Schedule.Contracts.Events;

/// <summary>
/// The audit event types Schedule produces (TASK-046; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class ScheduleAuditEvents
{
    /// <summary>DATA_CHANGE: the project's schedule was created.</summary>
    public const string ScheduleInitialized = "Schedule.ScheduleInitialized";

    /// <summary>DATA_CHANGE: an activity was added, with the number of activities whose dates the recalculation moved.</summary>
    public const string ActivityCreated = "Schedule.ActivityCreated";

    /// <summary>DATA_CHANGE: an activity's inputs changed (the name withheld).</summary>
    public const string ActivityChanged = "Schedule.ActivityChanged";

    /// <summary>LIFECYCLE_TRANSITION: PLANNED → CANCELLED.</summary>
    public const string ActivityCancelled = "Schedule.ActivityCancelled";

    /// <summary>DATA_CHANGE: a leaf's Current Forecast changed.</summary>
    public const string ActivityReforecast = "Schedule.ActivityReforecast";

    /// <summary>DATA_CHANGE.</summary>
    public const string DependencyCreated = "Schedule.DependencyCreated";

    /// <summary>DATA_CHANGE.</summary>
    public const string DependencyDeleted = "Schedule.DependencyDeleted";

    /// <summary>DATA_CHANGE: a DRAFT candidate was opened.</summary>
    public const string BaselineCreated = "Schedule.BaselineCreated";

    /// <summary>DATA_CHANGE: a DRAFT candidate was deleted.</summary>
    public const string BaselineDeleted = "Schedule.BaselineDeleted";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT or RETURNED → SUBMITTED, with the WF-11 run.</summary>
    public const string BaselineSubmitted = "Schedule.BaselineSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: → ACTIVE, with the baseline it superseded and whether WF-11 approved it.</summary>
    public const string BaselineActivated = "Schedule.BaselineActivated";

    /// <summary>LIFECYCLE_TRANSITION: ACTIVE → SUPERSEDED, with the baseline that superseded it.</summary>
    public const string BaselineSuperseded = "Schedule.BaselineSuperseded";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → RETURNED, by WF-11 or because the approved candidate can no longer activate.</summary>
    public const string BaselineReturned = "Schedule.BaselineReturned";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → REJECTED.</summary>
    public const string BaselineRejected = "Schedule.BaselineRejected";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → WITHDRAWN.</summary>
    public const string BaselineWithdrawn = "Schedule.BaselineWithdrawn";

    /// <summary>LIFECYCLE_TRANSITION, FAILED: a WF-11 outcome for a revision that is no longer under review (EV-5).</summary>
    public const string ApprovalOutcomeIgnored = "Schedule.ApprovalOutcomeIgnored";

    /// <summary>DATA_CHANGE: ADR-014's Declared Baseline was recorded ACTIVE from an intake.</summary>
    public const string DeclaredBaselineRecorded = "Schedule.DeclaredBaselineRecorded";

    /// <summary>DATA_CHANGE: the CURRENT/LIVE Schedule Health or finish variance changed.</summary>
    public const string ScheduleHealthRecomputed = "Schedule.ScheduleHealthRecomputed";
}
