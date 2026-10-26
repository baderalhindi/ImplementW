namespace PMPlatform.Application.Features.ExternalParticipation.Contracts;

/// <summary>
/// The ExternalParticipation module's error codes (api-conventions R-27), named as WF-13's error catalogue (§22) names them where it has
/// one. A code, once shipped, keeps its meaning.
/// </summary>
public static class ExternalParticipationErrorCodes
{
    /// <summary>422: a request is drafted and issued for a project that is APPROVED_PLANNED, ACTIVE or SUSPENDED (WF-13 §11.1, DCL-EXT-16).</summary>
    public const string ProjectNotEligible = "PROJECT_STATE_NOT_PERMITTED";

    /// <summary>422: the entity is not ACTIVE (EXT-ERR-002).</summary>
    public const string EntityInactive = "EXTERNAL_ENTITY_INACTIVE";

    /// <summary>422: the contribution type is not a PUBLISHED item of CONTRIBUTION_TYPE, or the platform has no typed schema for its code (EXT-ERR-015).</summary>
    public const string SchemaInvalid = "EXTERNAL_REQUEST_SCHEMA_INVALID";

    /// <summary>422: the PARTICIPATION version in force does not enable the contribution type for the project's participation mode (ADR-013).</summary>
    public const string ContributionTypeNotEnabled = "EXTERNAL_CONTRIBUTION_TYPE_NOT_ENABLED";

    /// <summary>
    /// 422: the source record does not fit the contribution type: missing where its schema applies to one, given where it is reference-only,
    /// or not a record of the request's project (EXT-ERR-016).
    /// </summary>
    public const string SourceInvalid = "EXTERNAL_REQUEST_SOURCE_INVALID";

    /// <summary>422: the responder is not an active external user of the request's entity whose access reaches the request (EXT-ERR-018).</summary>
    public const string ResponsibleUserIneligible = "EXTERNAL_RESPONSIBLE_USER_INELIGIBLE";

    /// <summary>422: the reviewer is not an internal user whose access lets them review the request (EXT-ERR-049).</summary>
    public const string ReviewerIneligible = "REVIEWER_INELIGIBLE";

    /// <summary>422: issuing a request without its responder or its reviewer.</summary>
    public const string RequestIncomplete = "EXTERNAL_REQUEST_INCOMPLETE";

    /// <summary>422: a due date before today on issue.</summary>
    public const string DueDateInvalid = "EXTERNAL_REQUEST_DUE_DATE_INVALID";

    /// <summary>409: the request is no longer a DRAFT: its purpose and source are those it was issued with; only a DRAFT is deleted (EXT-ERR-011).</summary>
    public const string RequestNotEditable = "EXTERNAL_REQUEST_NOT_EDITABLE";

    /// <summary>409: the request takes no answer now: it is not ISSUED, or it already has its answer (EXT-ERR-013).</summary>
    public const string RequestNotOpen = "EXTERNAL_REQUEST_NOT_OPEN";

    /// <summary>409: the request's answer is with AHDA's reviewer; it is decided, not cancelled.</summary>
    public const string RequestResponded = "EXTERNAL_REQUEST_RESPONDED";

    /// <summary>409: the revision is no longer a DRAFT: what was submitted is never edited, by anyone (EXT-ERR-020, BR-EXT-010).</summary>
    public const string ContributionNotEditable = "CONTRIBUTION_NOT_EDITABLE";

    /// <summary>409: the revision was submitted already (EXT-ERR-021).</summary>
    public const string ContributionAlreadySubmitted = "CONTRIBUTION_ALREADY_SUBMITTED";

    /// <summary>409: the revision was accepted, returned or rejected already (EXT-ERR-027).</summary>
    public const string ContributionAlreadyDecided = "CONTRIBUTION_ALREADY_DECIDED";

    /// <summary>422: a field outside the contribution type's typed schema, or one given twice (EXT-ERR-046, BR-EXT-020).</summary>
    public const string FieldNotAllowed = "EXTERNAL_FIELD_ACCESS_DENIED";

    /// <summary>422: a value that is not of its field's type or range (EXT-ERR-022).</summary>
    public const string ValidationFailed = "CONTRIBUTION_VALIDATION_FAILED";

    /// <summary>422: submitting without a required field (EXT-ERR-023).</summary>
    public const string RequiredItemMissing = "CONTRIBUTION_REQUIRED_ITEM_MISSING";

    /// <summary>409: only a revision ACCEPTED_PENDING_APPLICATION is applied (EXT-ERR-038).</summary>
    public const string ApplicationNotPermitted = "SOURCE_APPLICATION_NOT_PERMITTED";

    /// <summary>409: the revision was applied already; it is never applied twice (EXT-ERR-040, EXT-CC-18).</summary>
    public const string ApplicationAlreadyCompleted = "SOURCE_APPLICATION_ALREADY_COMPLETED";

    /// <summary>409: the last attempt found the source changed; an AHDA user revalidates it before it is applied (EXT-ERR-042, BR-EXT-022).</summary>
    public const string ApplicationConflict = "SOURCE_APPLICATION_CONFLICT";

    /// <summary>409: only the latest attempt of a revision awaiting application, ended in CONFLICT and not yet revalidated, is revalidated.</summary>
    public const string ApplicationNotRevalidatable = "SOURCE_APPLICATION_NOT_REVALIDATABLE";

    /// <summary>A FAILED attempt's code, and 422 on a revalidation: the source record no longer exists (EXT-ERR-036).</summary>
    public const string SourceRecordNotFound = "SOURCE_RECORD_NOT_FOUND";

    /// <summary>A FAILED attempt's code: the source record's state ends what it can take — a CANCELLED task (WF-13 SOURCE_TERMINAL).</summary>
    public const string SourceRecordTerminal = "SOURCE_RECORD_TERMINAL";

    /// <summary>A FAILED attempt's code: the source's own rules refuse the change now; it may be applied once they allow it (EXT-ERR-037).</summary>
    public const string SourceRecordStateInvalid = "SOURCE_RECORD_STATE_INVALID";

    /// <summary>422 (R-37): the <c>Idempotency-Key</c> already made an attempt, for another contribution.</summary>
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
}
