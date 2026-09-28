using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>An audit event with its attributes, and its forwarding record if its class is forwarded to the SIEM.</summary>
public sealed record AuditRecord(AuditEvent Event, IReadOnlyList<AuditEventAttribute> Attributes, AuditForwardingRecord? Forwarding);
