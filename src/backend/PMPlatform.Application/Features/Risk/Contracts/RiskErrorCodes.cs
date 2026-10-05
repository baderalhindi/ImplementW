namespace PMPlatform.Application.Features.Risk.Contracts;

/// <summary>The Risk module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class RiskErrorCodes
{
    /// <summary>
    /// 422: risks are registered while the project is APPROVED_PLANNED, ACTIVE or SUSPENDED, and changed while it is one of those
    /// or COMPLETED; never before approval or once CLOSED.
    /// </summary>
    public const string ProjectNotEligible = "RISK_PROJECT_NOT_ELIGIBLE";

    /// <summary>422: the project's governance profile carries no risk management (ADR-015: Light carries issues only).</summary>
    public const string NotInProfile = "RISK_NOT_IN_PROFILE";

    /// <summary>422: the category is not a PUBLISHED item of the RISK_CATEGORY catalogue.</summary>
    public const string CategoryInvalid = "RISK_CATEGORY_INVALID";

    /// <summary>422: the owner of the risk or of an action holds no role over the project now.</summary>
    public const string OwnerNotEligible = "RISK_OWNER_NOT_ELIGIBLE";

    /// <summary>422: a risk is identified on or before today, never in the future.</summary>
    public const string IdentifiedDateInvalid = "RISK_IDENTIFIED_DATE_INVALID";

    /// <summary>409: the risk is CLOSED and changes no more until it is reopened.</summary>
    public const string Closed = "RISK_CLOSED";

    /// <summary>
    /// 422: the assessment does not fit the RISK_MATRIX version in force: a probability level it does not define, a dimension
    /// it defines missing or given twice, a dimension it does not define, or a level it does not define for the dimension.
    /// </summary>
    public const string ImpactInvalid = "RISK_IMPACT_INVALID";

    /// <summary>422: treatment starts with at least one PLANNED or IN_PROGRESS treatment action.</summary>
    public const string TreatmentActionRequired = "RISK_TREATMENT_ACTION_REQUIRED";

    /// <summary>409: the risk is accepted until its acceptance expires or is revoked: it is not accepted twice, and not treated meanwhile.</summary>
    public const string AcceptanceActive = "RISK_ACCEPTANCE_ACTIVE";

    /// <summary>409: the risk has no ACTIVE acceptance to revoke.</summary>
    public const string AcceptanceNotActive = "RISK_ACCEPTANCE_NOT_ACTIVE";

    /// <summary>422: an acceptance expires after the day it is given; there is no permanent acceptance (TASK-055 gate decision).</summary>
    public const string AcceptanceExpiryInvalid = "RISK_ACCEPTANCE_EXPIRY_INVALID";

    /// <summary>409: an issue has already been raised from the risk.</summary>
    public const string AlreadyMaterialised = "RISK_ALREADY_MATERIALISED";

    /// <summary>409: the treatment action is COMPLETED or CANCELLED and changes no more.</summary>
    public const string ActionNotEditable = "RISK_ACTION_NOT_EDITABLE";
}
