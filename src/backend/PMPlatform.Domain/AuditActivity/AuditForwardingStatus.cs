namespace PMPlatform.Domain.AuditActivity;

/// <summary>
/// PENDING until the SIEM accepts the event, then FORWARDED. FAILED means the last attempt was refused or did not
/// reach the SIEM; the event is tried again on the next pass.
/// </summary>
public enum AuditForwardingStatus
{
    Pending = 1,
    Forwarded = 2,
    Failed = 3,
}
