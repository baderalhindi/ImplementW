namespace PMPlatform.Api.Errors;

/// <summary>The platform codes of the api-conventions §4.4 catalogue this API returns so far.</summary>
internal static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string AuthenticationRequired = "AUTHENTICATION_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string Unavailable = "UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";
}
