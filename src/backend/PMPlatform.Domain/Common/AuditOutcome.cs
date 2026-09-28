namespace PMPlatform.Domain.Common;

/// <summary>How the audited occurrence ended (ERD <c>audit_event.outcome</c>).</summary>
public enum AuditOutcome
{
    Success = 1,

    /// <summary>Refused by an authorization rule.</summary>
    Denied = 2,

    /// <summary>Attempted and not achieved: a rejected credential, an unreachable provider.</summary>
    Failed = 3,
}
