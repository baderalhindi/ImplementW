namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>One field a rule refused, as an <c>errors[]</c> item: the request field and why, never the value (R-25).</summary>
public sealed record FieldIssue(string Field, string Code)
{
    /// <summary>No such record.</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>The record exists but may not be used here: disabled, retired, not internal.</summary>
    public const string Inactive = "INACTIVE";

    /// <summary>Another record already holds this value.</summary>
    public const string Duplicate = "DUPLICATE";

    /// <summary>Required by the rule named in the error code.</summary>
    public const string Required = "REQUIRED";

    /// <summary>A date that must follow another does not (api-conventions R-23).</summary>
    public const string BeforeStart = "DATE_BEFORE_START";

    /// <summary>Not permitted by the rule named in the error code.</summary>
    public const string NotAllowed = "NOT_ALLOWED";

    /// <summary>ADR-013: outside the external user's own entity.</summary>
    public const string OutsideEntity = "OUTSIDE_ENTITY";

    /// <summary>ADR-013: the role may not be held by an external user.</summary>
    public const string NotExternalEligible = "NOT_EXTERNAL_ELIGIBLE";
}
