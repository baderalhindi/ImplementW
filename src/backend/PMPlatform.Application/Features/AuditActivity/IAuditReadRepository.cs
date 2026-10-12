using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// An audit event with the anchors it is authorised on (M-7): its project's owning department and Project Manager, and its entity
/// (the event's own, else its project's). They are read as they are now, so audit and Activity follow current access (FG-06 §6.2).
/// </summary>
public sealed record AnchoredAuditEvent(AuditEvent Event, Guid? DepartmentId, Guid? ProjectManagerUserId, Guid? ExternalEntityId, string? ActorDisplayName)
{
    public AuthorizationSubject Subject => new()
    {
        ProjectId = Event.ScopeProjectId,
        DepartmentId = DepartmentId,
        ExternalEntityId = ExternalEntityId,
        OwnerUserId = ProjectManagerUserId,
        DataClassificationItemId = Event.DataClassificationItemId,
    };
}

/// <summary>An audit category the caller reads, and the records its permission reaches.</summary>
public sealed record AuditReach(AuditCategory Category, RecordScope Scope);

/// <summary>An Activity line with its event's type and subject, and who its actor is now.</summary>
public sealed record AnchoredActivityEntry(
    BusinessActivityEntry Entry,
    string EventType,
    string? SubjectModule,
    string? SubjectType,
    Guid? SubjectId,
    string? ActorDisplayName,
    UserType? ActorUserType,
    Guid? ActorExternalEntityId);

/// <summary>The audit store and the Activity projection as ADM-050/051 and SCR-057 read them (TASK-073).</summary>
public interface IAuditReadRepository
{
    /// <summary>
    /// A page of the events in any of <paramref name="reach"/>'s categories that its scope reaches, matching the query, newest
    /// first. The scope is applied in the query, before anything is counted (AUD-CC-20).
    /// </summary>
    public Task<(IReadOnlyList<AnchoredAuditEvent> Items, int TotalCount)> PageEventsAsync(
        IReadOnlyList<AuditReach> reach, AuditEventQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AnchoredAuditEvent?> FindEventAsync(Guid auditEventId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<AuditEventAttribute>> ListAttributesAsync(Guid auditEventId, CancellationToken cancellationToken);

    public Task<AuditForwardingRecord?> FindForwardingAsync(Guid auditEventId, CancellationToken cancellationToken);

    /// <summary>A page of the Activity lines <paramref name="scope"/> reaches, matching the query, newest first.</summary>
    public Task<(IReadOnlyList<AnchoredActivityEntry> Items, int TotalCount)> PageActivityAsync(
        RecordScope scope, ActivityQuery query, PageRequest page, CancellationToken cancellationToken);
}
