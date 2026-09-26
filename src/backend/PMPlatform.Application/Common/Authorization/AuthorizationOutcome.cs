namespace PMPlatform.Application.Common.Authorization;

/// <summary>An authorization decision as the API answers it (api-conventions R-47).</summary>
public enum AuthorizationOutcome
{
    Allowed = 1,

    /// <summary>403 <c>PERMISSION_DENIED</c>: the caller may see the record but may not do this.</summary>
    Forbidden = 2,

    /// <summary>404 <c>NOT_FOUND</c>: the record is outside the caller's scope, answered exactly as a nonexistent id.</summary>
    NotFound = 3,
}
