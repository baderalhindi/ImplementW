using System.Diagnostics.CodeAnalysis;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// One audit event as the SIEM receives it (PTBC-029). The SIEM product is not yet named, so this is the platform's own
/// contract and the selected product is fitted to it by its adapter. Values are formatted as the audit store holds them.
/// Delivery is at least once: <see cref="EventId"/> identifies a repeated delivery.
/// </summary>
public sealed record SiemEvent(
    Guid EventId,
    string EventClass,
    string EventType,
    string Outcome,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    SiemActor Actor,
    SiemSubject? Subject,
    Guid? ScopeProjectId,
    Guid? ScopeExternalEntityId,
    Guid CorrelationId,
    string EventHash,
    string? PreviousEventHash,
    IReadOnlyList<SiemAttribute> Attributes);

public sealed record SiemActor(string ActorType, Guid? UserId);

public sealed record SiemSubject(string? Module, string? Type, Guid? Id);

[SuppressMessage("Naming", "CA1711", Justification = "The ERD's term: one audit_activity.audit_event_attribute row as the SIEM receives it.")]
public sealed record SiemAttribute(string Name, string? OldValue, string? NewValue);
