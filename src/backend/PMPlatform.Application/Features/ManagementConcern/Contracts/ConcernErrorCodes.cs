namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>The ManagementConcern module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class ConcernErrorCodes
{
    /// <summary>
    /// 422: concerns are raised while the project is APPROVED_PLANNED, ACTIVE or SUSPENDED, and changed while it is one of those or
    /// COMPLETED; never before approval or once CLOSED.
    /// </summary>
    public const string ProjectNotEligible = "CONCERN_PROJECT_NOT_ELIGIBLE";

    /// <summary>422: the category is not a PUBLISHED item of the CONCERN_CATEGORY catalogue.</summary>
    public const string CategoryInvalid = "CONCERN_CATEGORY_INVALID";

    /// <summary>422: the priority is not a PUBLISHED item of the PRIORITY catalogue.</summary>
    public const string PriorityInvalid = "CONCERN_PRIORITY_INVALID";

    /// <summary>
    /// 422: the impacts do not fit the scale of the RISK_MATRIX version in force: a dimension it does not define, a dimension given
    /// twice, or a level it does not define for the dimension.
    /// </summary>
    public const string ImpactInvalid = "CONCERN_IMPACT_INVALID";

    /// <summary>422: the assignee holds no role over the project now.</summary>
    public const string AssigneeNotEligible = "CONCERN_ASSIGNEE_NOT_ELIGIBLE";

    /// <summary>422: a target resolution date, when set or changed, is today or later.</summary>
    public const string TargetDateInvalid = "CONCERN_TARGET_DATE_INVALID";

    /// <summary>409: the concern is CLOSED and changes no more.</summary>
    public const string Closed = "CONCERN_CLOSED";

    /// <summary>409: the concern's resolution is with validation, or validated: its fields and impacts no longer change.</summary>
    public const string NotEditable = "CONCERN_NOT_EDITABLE";

    /// <summary>409: the concern already has an OPEN escalation; it is resolved or withdrawn before another, and before the concern closes.</summary>
    public const string EscalationOpen = "CONCERN_ESCALATION_OPEN";

    /// <summary>409: a RESOLVED or CLOSED concern is not escalated.</summary>
    public const string NotEscalatable = "CONCERN_NOT_ESCALATABLE";

    /// <summary>409: the escalation is RESOLVED or WITHDRAWN and changes no more.</summary>
    public const string EscalationNotOpen = "CONCERN_ESCALATION_NOT_OPEN";

    /// <summary>422 (R-37): the <c>Idempotency-Key</c> already raised an escalation, for another concern or with another reason.</summary>
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
}
