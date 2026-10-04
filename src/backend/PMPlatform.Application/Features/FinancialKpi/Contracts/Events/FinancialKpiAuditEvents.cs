namespace PMPlatform.Application.Features.FinancialKpi.Contracts.Events;

/// <summary>
/// The audit event types FinancialKpi produces (TASK-052; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. The versioned subjects — <c>FinancialCommitment</c> and <c>KpiTargetVersion</c> — share the
/// <c>Version…</c> events, told apart by the audit subject's type.
/// </summary>
public static class FinancialKpiAuditEvents
{
    /// <summary>DATA_CHANGE: a financial field's source mode was set or changed.</summary>
    public const string SourceModeChanged = "FinancialKpi.SourceModeChanged";

    /// <summary>DATA_CHANGE: a DRAFT version was opened.</summary>
    public const string VersionCreated = "FinancialKpi.VersionCreated";

    /// <summary>DATA_CHANGE: a DRAFT or RETURNED version's figures changed.</summary>
    public const string VersionChanged = "FinancialKpi.VersionChanged";

    /// <summary>DATA_CHANGE: a DRAFT version was deleted.</summary>
    public const string VersionDeleted = "FinancialKpi.VersionDeleted";

    /// <summary>DATA_CHANGE: a referenced document was attached to a commitment version.</summary>
    public const string DocumentAttached = "FinancialKpi.DocumentAttached";

    /// <summary>DATA_CHANGE: a commitment version's referenced document was withdrawn.</summary>
    public const string DocumentWithdrawn = "FinancialKpi.DocumentWithdrawn";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT or RETURNED → SUBMITTED to WF-11, with the run.</summary>
    public const string VersionSubmitted = "FinancialKpi.VersionSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → ACTIVE on WF-11's approval, with the version it superseded.</summary>
    public const string VersionActivated = "FinancialKpi.VersionActivated";

    /// <summary>LIFECYCLE_TRANSITION: ACTIVE → SUPERSEDED, with its successor.</summary>
    public const string VersionSuperseded = "FinancialKpi.VersionSuperseded";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → RETURNED, REJECTED or WITHDRAWN by WF-11's outcome.</summary>
    public const string VersionReturned = "FinancialKpi.VersionReturned";

    public const string VersionRejected = "FinancialKpi.VersionRejected";

    public const string VersionWithdrawn = "FinancialKpi.VersionWithdrawn";

    /// <summary>LIFECYCLE_TRANSITION, FAILED: a WF-11 outcome for a revision no longer under review (EV-5).</summary>
    public const string ApprovalOutcomeIgnored = "FinancialKpi.ApprovalOutcomeIgnored";

    /// <summary>DATA_CHANGE: a period's financial update was opened (DRAFT).</summary>
    public const string UpdateStarted = "FinancialKpi.UpdateStarted";

    /// <summary>DATA_CHANGE: a DRAFT update's figures changed (the narrative withheld).</summary>
    public const string UpdateChanged = "FinancialKpi.UpdateChanged";

    public const string UpdateDeleted = "FinancialKpi.UpdateDeleted";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT → SUBMITTED.</summary>
    public const string UpdateSubmitted = "FinancialKpi.UpdateSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → UNDER_REVIEW.</summary>
    public const string UpdateReviewStarted = "FinancialKpi.UpdateReviewStarted";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → RETURNED.</summary>
    public const string UpdateReturned = "FinancialKpi.UpdateReturned";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → PUBLISHED, with the snapshot, its status and the pinned thresholds.</summary>
    public const string UpdatePublished = "FinancialKpi.UpdatePublished";

    /// <summary>AUTHORIZATION_DENIAL: an external user or the submitter tried to review or publish (ADR-013).</summary>
    public const string ReviewRefused = "FinancialKpi.ReviewRefused";

    /// <summary>DATA_CHANGE: a KPI was assigned to a project, or its assignment's owner or frequency changed.</summary>
    public const string KpiAssigned = "FinancialKpi.KpiAssigned";

    public const string AssignmentChanged = "FinancialKpi.AssignmentChanged";

    /// <summary>LIFECYCLE_TRANSITION: an assignment suspended, reactivated or retired.</summary>
    public const string AssignmentTransitioned = "FinancialKpi.AssignmentTransitioned";

    /// <summary>DATA_CHANGE: a measurement was recorded (DRAFT), with the target version it is pinned to.</summary>
    public const string MeasurementRecorded = "FinancialKpi.MeasurementRecorded";

    public const string MeasurementChanged = "FinancialKpi.MeasurementChanged";

    public const string MeasurementDeleted = "FinancialKpi.MeasurementDeleted";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT → SUBMITTED.</summary>
    public const string MeasurementSubmitted = "FinancialKpi.MeasurementSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → PUBLISHED.</summary>
    public const string MeasurementPublished = "FinancialKpi.MeasurementPublished";
}
