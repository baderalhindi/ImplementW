namespace PMPlatform.Api.Errors;

/// <summary>The platform codes of the api-conventions §4.4 catalogue this API returns so far.</summary>
internal static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
    public const string IdempotencyKeyInvalid = "IDEMPOTENCY_KEY_INVALID";
    public const string AuthenticationRequired = "AUTHENTICATION_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string StepUpRequired = "STEP_UP_REQUIRED";
    public const string NotFound = "NOT_FOUND";
    public const string InvalidTransition = "INVALID_TRANSITION";
    public const string TerminalState = "TERMINAL_STATE";
    public const string PreconditionFailed = "PRECONDITION_FAILED";
    public const string PreconditionRequired = "PRECONDITION_REQUIRED";
    public const string Unavailable = "UNAVAILABLE";

    /// <summary>422: required configuration is absent or ambiguous; the operation fails closed (TASK-034, Blueprint Section 12).</summary>
    public const string ConfigurationMissing = "CONFIGURATION_MISSING";
    public const string InternalError = "INTERNAL_ERROR";
}
