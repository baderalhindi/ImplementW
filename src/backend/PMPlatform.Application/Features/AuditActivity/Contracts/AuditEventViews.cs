using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.AuditActivity.Contracts;

/// <summary>One audit event as ADM-050 lists it (FG-06 §8.2). The actor's name is today's; the actor's id is the historical identity.</summary>
public sealed record AuditEventSummary(
    Guid Id,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    AuditCategory Category,
    AuditEventClass EventClass,
    string EventType,
    AuditOutcome Outcome,
    AuditActorType ActorType,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string? SubjectModule,
    string? SubjectType,
    Guid? SubjectId,
    Guid? ScopeProjectId,
    Guid? ScopeExternalEntityId,
    Guid CorrelationId,
    Guid? DataClassificationItemId);

public sealed record AuditEventPage(IReadOnlyList<AuditEventSummary> Items, int Page, int PageSize, int TotalCount);

/// <summary>A captured value; a request-context value the caller may not see is left out and named in <see cref="AuditEventDetail.MaskedAttributes"/>.</summary>
public sealed record AuditAttributeView(string Name, string? OldValue, string? NewValue);

/// <summary>What makes the event tamper-evident (FG-06 §8.3): its place in the hash chain and the digest of its attributes.</summary>
public sealed record AuditEventIntegrity(string EventHash, string? PreviousEventHash, string? AttributeDigest);

/// <summary>Whether the event has reached the SIEM (PTBC-029); null for a class that is not forwarded.</summary>
public sealed record AuditEventForwarding(AuditForwardingStatus Status, DateTimeOffset? ForwardedAt);

/// <summary>ADM-051: the immutable event with its attributes, integrity metadata and forwarding state (FG-06 §8.3).</summary>
public sealed record AuditEventDetail(
    AuditEventSummary Event,
    IReadOnlyList<AuditAttributeView> Attributes,
    IReadOnlyList<string> MaskedAttributes,
    AuditEventIntegrity Integrity,
    AuditEventForwarding? Forwarding);

/// <summary>
/// What the caller may do with audit and Activity (AUD-API-014): the categories they may read, whether they see request context,
/// whether they read Activity, and whether they operate the store. The SPA shows what this allows; the API enforces it again.
/// </summary>
public sealed record AuditAccessView(
    IReadOnlyList<AuditCategory> Categories,
    bool RequestContextVisible,
    bool ActivityVisible,
    bool OperationsPermitted);

/// <summary>
/// ADM-050's server-side filters (FG-06 §8.2). <see cref="Categories"/> empty means every category the caller may read; naming one
/// they may not is refused. Results are newest first.
/// </summary>
public sealed record AuditEventQuery
{
    public IReadOnlyCollection<AuditCategory> Categories { get; init; } = [];

    public DateTimeOffset? OccurredFrom { get; init; }

    public DateTimeOffset? OccurredTo { get; init; }

    public Guid? ActorUserId { get; init; }

    public IReadOnlyCollection<AuditEventClass> EventClasses { get; init; } = [];

    public string? EventType { get; init; }

    public IReadOnlyCollection<AuditOutcome> Outcomes { get; init; } = [];

    public Guid? ProjectId { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public Guid? CorrelationId { get; init; }
}
