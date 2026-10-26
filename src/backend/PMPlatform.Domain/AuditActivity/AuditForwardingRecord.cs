using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.AuditActivity;

/// <summary>
/// Whether an <see cref="AuditEvent"/> of a forwarded class has reached the SIEM (PTBC-029), kept apart from the event
/// so the event row is never updated. Delete policy: RETAIN.
/// </summary>
public sealed class AuditForwardingRecord : AuditedEntity
{
    public Guid AuditEventId { get; set; }

    public DateTimeOffset? ForwardedAt { get; set; }

    /// <summary>The IntegrationMonitoring invocation of the delivery, once that module exists (TASK-075).</summary>
    public Guid? InvocationId { get; set; }

    public AuditForwardingStatus Status { get; set; }
}
