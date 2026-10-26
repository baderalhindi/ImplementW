namespace PMPlatform.Application.Features.Risk.Contracts.Events;

/// <summary>
/// The audit event types Risk produces (TASK-055; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the producer's
/// unit of work. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class RiskAuditEvents
{
    /// <summary>DATA_CHANGE: a risk was registered, IDENTIFIED.</summary>
    public const string RiskRegistered = "Risk.RiskRegistered";

    /// <summary>DATA_CHANGE: a risk's title, description, category, owner or dates changed (title and description withheld).</summary>
    public const string RiskChanged = "Risk.RiskChanged";

    /// <summary>DATA_CHANGE: an assessment version was recorded, with its pinned matrix version and rating; IDENTIFIED → ASSESSED on the first.</summary>
    public const string RiskAssessed = "Risk.RiskAssessed";

    /// <summary>LIFECYCLE_TRANSITION: ASSESSED or MONITORING → TREATMENT.</summary>
    public const string TreatmentStarted = "Risk.TreatmentStarted";

    /// <summary>LIFECYCLE_TRANSITION: ASSESSED or TREATMENT → MONITORING.</summary>
    public const string MonitoringStarted = "Risk.MonitoringStarted";

    /// <summary>LIFECYCLE_TRANSITION: an acceptance was given until its expiry; the risk → MONITORING.</summary>
    public const string RiskAccepted = "Risk.RiskAccepted";

    /// <summary>LIFECYCLE_TRANSITION: an acceptance was revoked; the risk → ASSESSED, back for review.</summary>
    public const string AcceptanceRevoked = "Risk.AcceptanceRevoked";

    /// <summary>LIFECYCLE_TRANSITION: an acceptance reached its expiry; the risk → ASSESSED, back for review. Recorded by the service principal.</summary>
    public const string AcceptanceExpired = "Risk.AcceptanceExpired";

    /// <summary>LIFECYCLE_TRANSITION: → CLOSED, with the rationale withheld; an ACTIVE acceptance ends with it.</summary>
    public const string RiskClosed = "Risk.RiskClosed";

    /// <summary>LIFECYCLE_TRANSITION: CLOSED → ASSESSED, or IDENTIFIED if never assessed, with the risk's reopen count.</summary>
    public const string RiskReopened = "Risk.RiskReopened";

    /// <summary>DATA_CHANGE: an issue was raised from the risk (edge 15), with its id.</summary>
    public const string RiskMaterialised = "Risk.RiskMaterialised";

    /// <summary>AUTHORIZATION_DENIAL: an assessment or acceptance refused to an external user (ADR-013).</summary>
    public const string AuthorityRefused = "Risk.AuthorityRefused";

    /// <summary>DATA_CHANGE.</summary>
    public const string TreatmentActionCreated = "Risk.TreatmentActionCreated";

    /// <summary>DATA_CHANGE: an action's title, description, type, owner or due date changed (title and description withheld).</summary>
    public const string TreatmentActionChanged = "Risk.TreatmentActionChanged";

    /// <summary>LIFECYCLE_TRANSITION: PLANNED → IN_PROGRESS.</summary>
    public const string TreatmentActionStarted = "Risk.TreatmentActionStarted";

    /// <summary>LIFECYCLE_TRANSITION: IN_PROGRESS → COMPLETED.</summary>
    public const string TreatmentActionCompleted = "Risk.TreatmentActionCompleted";

    /// <summary>LIFECYCLE_TRANSITION: PLANNED or IN_PROGRESS → CANCELLED.</summary>
    public const string TreatmentActionCancelled = "Risk.TreatmentActionCancelled";
}
