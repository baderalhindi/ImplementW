namespace PMPlatform.Application.Features.AuditActivity.Contracts.Events;

/// <summary>
/// AuditActivity's own audit events (event-conventions row 30; FG-06 §6.4, §23): access to sensitive audit detail, and each
/// operation on the store. All are PRIVILEGED_ACTION. Routine search and paging are not audited (BR-AUD-051).
/// </summary>
public static class AuditActivityAuditEvents
{
    /// <summary>ADM-051 opened on a SECURITY event, a classified event, or with request context revealed (AUD-EVT-027).</summary>
    public const string SensitiveDetailViewed = "AuditActivity.SensitiveDetailViewed";

    /// <summary>The chain verified; outcome FAILED when it found tampering (AUD-EVT-013, -014).</summary>
    public const string IntegrityVerified = "AuditActivity.IntegrityVerified";

    /// <summary>The Activity projection compared with the store; outcome FAILED when they differ.</summary>
    public const string ActivityProjectionChecked = "AuditActivity.ActivityProjectionChecked";

    /// <summary>The Activity projection derived again from the store and repaired (AUD-EVT-041).</summary>
    public const string ActivityProjectionRebuilt = "AuditActivity.ActivityProjectionRebuilt";
}
