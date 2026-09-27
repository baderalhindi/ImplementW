namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>Why an administration request was refused; the API turns each into its api-conventions §4.4 status and code.</summary>
public enum AdministrationErrorKind
{
    /// <summary>404 <c>NOT_FOUND</c>: no such record, or one outside the caller's scope (R-47).</summary>
    NotFound = 1,

    /// <summary>403 <c>PERMISSION_DENIED</c>.</summary>
    Forbidden = 2,

    /// <summary>422 with a module code: well formed, refused by a rule.</summary>
    RuleViolated = 3,

    /// <summary>409 with a module code: a unique key or a single-instance rule.</summary>
    Conflict = 4,

    /// <summary>409 <c>INVALID_TRANSITION</c>: the command is not allowed from the current state.</summary>
    InvalidTransition = 5,

    /// <summary>409 <c>TERMINAL_STATE</c>: the record is in a terminal state and takes no write.</summary>
    TerminalState = 6,

    /// <summary>412 <c>PRECONDITION_FAILED</c>: the record changed since the version the caller holds (R-21).</summary>
    PreconditionFailed = 7,

    /// <summary>503 <c>UNAVAILABLE</c>: a provider the operation needs is not configured or not reachable.</summary>
    Unavailable = 8,
}
