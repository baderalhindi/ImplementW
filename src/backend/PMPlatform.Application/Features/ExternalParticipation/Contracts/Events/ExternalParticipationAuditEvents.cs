namespace PMPlatform.Application.Features.ExternalParticipation.Contracts.Events;

/// <summary>
/// The audit event types ExternalParticipation produces (TASK-066; event-conventions EV-1; WF-13 §23.2), recorded through <c>IAuditTrail</c>
/// in the producer's unit of work, every one with the request as subject so a request's history is one query. Instructions, answers and
/// reasons are free text and are not copied. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class ExternalParticipationAuditEvents
{
    /// <summary>DATA_CHANGE: a request was drafted.</summary>
    public const string RequestCreated = "ExternalParticipation.RequestCreated";

    /// <summary>DATA_CHANGE: a DRAFT request's purpose, source, people or due date changed (instructions withheld).</summary>
    public const string RequestChanged = "ExternalParticipation.RequestChanged";

    /// <summary>DATA_CHANGE: a DRAFT request was deleted (HARD_DRAFT).</summary>
    public const string RequestDeleted = "ExternalParticipation.RequestDeleted";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT → ISSUED, with its responder, reviewer and due date.</summary>
    public const string RequestIssued = "ExternalParticipation.RequestIssued";

    /// <summary>LIFECYCLE_TRANSITION: ISSUED or IN_PROGRESS → CANCELLED (reason withheld).</summary>
    public const string RequestCancelled = "ExternalParticipation.RequestCancelled";

    /// <summary>DATA_CHANGE: an issued request's responder was replaced; earlier revisions keep their contributor (BR-EXT-030).</summary>
    public const string ResponderAssigned = "ExternalParticipation.ResponderAssigned";

    /// <summary>DATA_CHANGE: an issued request's reviewer was replaced.</summary>
    public const string ReviewerAssigned = "ExternalParticipation.ReviewerAssigned";

    /// <summary>DATA_CHANGE: the responder drafted revision 1 of the answer.</summary>
    public const string ContributionStarted = "ExternalParticipation.ContributionStarted";

    /// <summary>DATA_CHANGE: a DRAFT revision's values were replaced (the codes of the fields, not their values).</summary>
    public const string ContributionChanged = "ExternalParticipation.ContributionChanged";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT → SUBMITTED, with the source version and state it was answered against.</summary>
    public const string ContributionSubmitted = "ExternalParticipation.ContributionSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → UNDER_REVIEW.</summary>
    public const string ReviewStarted = "ExternalParticipation.ReviewStarted";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → ACCEPTED_PENDING_APPLICATION, or APPLIED for a reference-only answer. Not an approval (BR-EXT-014).</summary>
    public const string ContributionAccepted = "ExternalParticipation.ContributionAccepted";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → RETURNED, with the next revision it opened (reasons withheld).</summary>
    public const string ContributionReturned = "ExternalParticipation.ContributionReturned";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → REJECTED, which closes the request (reasons withheld).</summary>
    public const string ContributionRejected = "ExternalParticipation.ContributionRejected";

    /// <summary>LIFECYCLE_TRANSITION: an attempt applied the revision to its source record, and the request closed.</summary>
    public const string SourceApplied = "ExternalParticipation.SourceApplied";

    /// <summary>DATA_CHANGE: an attempt found the source record changed since the answer and applied nothing (BR-EXT-022).</summary>
    public const string SourceApplicationConflict = "ExternalParticipation.SourceApplicationConflict";

    /// <summary>DATA_CHANGE: the source's own rules refused an attempt and it applied nothing; a terminal refusal ends the revision APPLICATION_FAILED.</summary>
    public const string SourceApplicationFailed = "ExternalParticipation.SourceApplicationFailed";

    /// <summary>DATA_CHANGE: an AHDA user confirmed that the accepted values apply to the source record as it now is.</summary>
    public const string SourceApplicationRevalidated = "ExternalParticipation.SourceApplicationRevalidated";

    /// <summary>AUTHORIZATION_DENIAL: requesting, reviewing or applying refused to an external user, whatever they hold (ADR-013).</summary>
    public const string AuthorityRefused = "ExternalParticipation.AuthorityRefused";
}
