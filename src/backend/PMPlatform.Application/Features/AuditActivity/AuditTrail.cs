using PMPlatform.Application.Common.Auditing;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// Turns an <see cref="AuditEntry"/> into the <c>audit_activity</c> rows (TASK-033). The client address is recorded with
/// every event; an event of a forwarded class gets a PENDING forwarding record in the same unit of work, so it cannot be
/// stored without being queued for the SIEM.
/// </summary>
internal sealed class AuditTrail(IAuditEventRepository events, IAuditRequestContext request, SiemForwardingPolicy forwarding, TimeProvider timeProvider)
    : IAuditTrail
{
    /// <summary>
    /// The SERVICE principal (db/seed) an event is attributed to when no user is known: a refused anonymous sign-in, a
    /// token that failed validation (ERD D-2: <c>created_by</c> is never empty).
    /// </summary>
    public static readonly Guid AuditCapturePrincipalId = new("00000000-0000-4000-8000-0000000000fd");

    internal const string ClientAddressAttribute = "client_address";

    public void Stage(AuditEntry entry) => events.Stage(Build(entry));

    public Task RecordAsync(AuditEntry entry) => events.AppendAsync(Build(entry));

    private AuditRecord Build(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid recordedBy = entry.ActorUserId ?? AuditCapturePrincipalId;
        AuditEvent auditEvent = new()
        {
            Id = Guid.CreateVersion7(now),
            OccurredAt = now,
            EventClass = entry.EventClass,
            EventType = entry.EventType,
            ActorType = entry.ActorType,
            ActorUserId = entry.ActorUserId,
            SubjectModule = entry.Subject?.Module,
            SubjectType = entry.Subject?.Type,
            SubjectId = entry.Subject?.Id,
            ScopeProjectId = entry.ScopeProjectId,
            ScopeExternalEntityId = entry.ScopeExternalEntityId,
            CorrelationId = request.CorrelationId,
            Outcome = entry.Outcome,
            DataClassificationItemId = entry.DataClassificationItemId,
            CreatedAt = now,
            CreatedBy = recordedBy,
            UpdatedAt = now,
            UpdatedBy = recordedBy,
        };

        IEnumerable<AuditAttribute> attributes = request.ClientAddress is { } address
            ? entry.Attributes.Append(AuditAttribute.Of(ClientAddressAttribute, address))
            : entry.Attributes;

        return new AuditRecord(
            auditEvent,
            [.. attributes.Select(a => new AuditEventAttribute
            {
                Id = Guid.CreateVersion7(now),
                AuditEventId = auditEvent.Id,
                AttributeName = a.Name,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                CreatedAt = now,
                CreatedBy = recordedBy,
                UpdatedAt = now,
                UpdatedBy = recordedBy,
            })],
            forwarding.Forwards(entry.EventClass)
                ? new AuditForwardingRecord
                {
                    Id = Guid.CreateVersion7(now),
                    AuditEventId = auditEvent.Id,
                    Status = AuditForwardingStatus.Pending,
                    CreatedAt = now,
                    CreatedBy = recordedBy,
                    UpdatedAt = now,
                    UpdatedBy = recordedBy,
                }
                : null);
    }
}
