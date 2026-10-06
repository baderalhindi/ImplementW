namespace PMPlatform.Application.Features.ManagementConcern.Contracts.Events;

/// <summary>
/// The audit event types ManagementConcern produces (TASK-057; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class ConcernAuditEvents
{
    /// <summary>DATA_CHANGE: a concern was raised, OPEN, with its computed severity if it was assessed; from a risk, with its id.</summary>
    public const string ConcernRaised = "ManagementConcern.ConcernRaised";

    /// <summary>DATA_CHANGE: a concern's title, description, category, priority or target date changed (title and description withheld).</summary>
    public const string ConcernChanged = "ManagementConcern.ConcernChanged";

    /// <summary>DATA_CHANGE: a concern's impacts were replaced and its severity recomputed, with the pinned version.</summary>
    public const string ConcernAssessed = "ManagementConcern.ConcernAssessed";

    /// <summary>LIFECYCLE_TRANSITION: OPEN → ASSIGNED, or a reassignment.</summary>
    public const string ConcernAssigned = "ManagementConcern.ConcernAssigned";

    /// <summary>LIFECYCLE_TRANSITION: ASSIGNED → IN_PROGRESS.</summary>
    public const string WorkStarted = "ManagementConcern.WorkStarted";

    /// <summary>LIFECYCLE_TRANSITION: IN_PROGRESS → PENDING_VALIDATION, with the WF-11 run (resolution withheld).</summary>
    public const string ResolutionSubmitted = "ManagementConcern.ResolutionSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: PENDING_VALIDATION → RESOLVED, by WF-11's approval.</summary>
    public const string ResolutionValidated = "ManagementConcern.ResolutionValidated";

    /// <summary>LIFECYCLE_TRANSITION: PENDING_VALIDATION → IN_PROGRESS, by WF-11's return, rejection or withdrawal; the next revision.</summary>
    public const string ResolutionReturned = "ManagementConcern.ResolutionReturned";

    /// <summary>LIFECYCLE_TRANSITION: RESOLVED → CLOSED.</summary>
    public const string ConcernClosed = "ManagementConcern.ConcernClosed";

    /// <summary>DATA_CHANGE: a review was recorded and the next one scheduled at the governance profile's cadence.</summary>
    public const string ConcernReviewed = "ManagementConcern.ConcernReviewed";

    /// <summary>LIFECYCLE_TRANSITION, on the concern: an escalation was raised, with its number and addressed role.</summary>
    public const string ConcernEscalated = "ManagementConcern.ConcernEscalated";

    /// <summary>LIFECYCLE_TRANSITION, on the concern: an escalation was resolved (resolution withheld).</summary>
    public const string EscalationResolved = "ManagementConcern.EscalationResolved";

    /// <summary>LIFECYCLE_TRANSITION, on the concern: an escalation was withdrawn by its escalator.</summary>
    public const string EscalationWithdrawn = "ManagementConcern.EscalationWithdrawn";

    /// <summary>AUTHORIZATION_DENIAL: management or escalation refused to an external user (ADR-013: intake and visibility only).</summary>
    public const string AuthorityRefused = "ManagementConcern.AuthorityRefused";

    /// <summary>DATA_CHANGE: a WF-11 outcome for a revision that is no longer under validation, recorded and not applied (EV-5).</summary>
    public const string OutcomeIgnored = "ManagementConcern.OutcomeIgnored";
}
