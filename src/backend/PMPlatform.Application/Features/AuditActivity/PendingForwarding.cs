using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>A forwarding record not yet FORWARDED, tracked for update, with the event it forwards.</summary>
public sealed record PendingForwarding(AuditForwardingRecord Forwarding, AuditEvent Event, IReadOnlyList<AuditEventAttribute> Attributes);
