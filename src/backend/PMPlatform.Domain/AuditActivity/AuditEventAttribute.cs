using System.Diagnostics.CodeAnalysis;
using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.AuditActivity;

/// <summary>
/// One named value captured with an <see cref="AuditEvent"/>: the old and new value of a change, or a fact about the
/// occurrence (only <see cref="NewValue"/>). Stored as the producer redacted it (event-conventions EV-9). Delete policy:
/// APPEND_ONLY.
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "ERD D-4: the entity is the PascalCase of its table, audit_activity.audit_event_attribute.")]
public sealed class AuditEventAttribute : AuditedEntity
{
    public Guid AuditEventId { get; set; }

    public required string AttributeName { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }
}
