namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>The IdentityAccess module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class IdentityAccessErrorCodes
{
    /// <summary>409: a username, email, directory subject, code or active directory reference another record already holds.</summary>
    public const string DuplicateKey = "IDENTITY_ACCESS_DUPLICATE_KEY";

    /// <summary>409: the user already holds this assignment, active and identical.</summary>
    public const string AlreadyAssigned = "IDENTITY_ACCESS_ALREADY_ASSIGNED";

    /// <summary>422: a referenced user, department, entity, entity type, project or profile version does not exist or may not be used.</summary>
    public const string ReferenceInvalid = "IDENTITY_ACCESS_REFERENCE_INVALID";

    /// <summary>422: an assignment to an external user breaks an ADR-013 rule; <c>errors[]</c> names the field and the rule.</summary>
    public const string ExternalGrantInvalid = "IDENTITY_ACCESS_EXTERNAL_GRANT_INVALID";

    /// <summary>422: only a PUBLISHED profile version may be assigned (ADR-018).</summary>
    public const string ProfileVersionNotPublished = "IDENTITY_ACCESS_PROFILE_VERSION_NOT_PUBLISHED";

    /// <summary>422: the project is CLOSED; access to it has ended (ADR-013).</summary>
    public const string ProjectClosed = "IDENTITY_ACCESS_PROJECT_CLOSED";

    /// <summary>422: an administrator may not disable themselves or change their own assignments.</summary>
    public const string SelfAdministration = "IDENTITY_ACCESS_SELF_ADMINISTRATION";

    /// <summary>422: a SERVICE principal is not administered through the API.</summary>
    public const string ServicePrincipal = "IDENTITY_ACCESS_SERVICE_PRINCIPAL";

    /// <summary>422: the directory is authoritative for this attribute of an internal user (ADR-007).</summary>
    public const string DirectoryAuthoritative = "IDENTITY_ACCESS_DIRECTORY_AUTHORITATIVE";

    /// <summary>422: the assignment starts in the past, or ends at or before its start.</summary>
    public const string InvalidPeriod = "IDENTITY_ACCESS_INVALID_PERIOD";

    /// <summary>422: the parent would make the department its own ancestor.</summary>
    public const string DepartmentCycle = "IDENTITY_ACCESS_DEPARTMENT_CYCLE";

    /// <summary>422: the user has no mobile number to verify.</summary>
    public const string MobileNumberMissing = "IDENTITY_ACCESS_MOBILE_NUMBER_MISSING";

    /// <summary>422: the verification code was not accepted for the user's current number (ADR-004).</summary>
    public const string MobileVerificationFailed = "IDENTITY_ACCESS_MOBILE_VERIFICATION_FAILED";
}
