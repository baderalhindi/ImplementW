using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>
/// The audit store and the Activity projection as ADM-050/051 and SCR-057 read them (TASK-073). Each event is joined to its
/// project's current anchors — owning department, Project Manager, delivering entity — and to its actor's current name and
/// employer. <c>project.project</c> and <c>identity_access.user</c> are read directly: AuditActivity is a foundation module and may
/// call no domain module (ADR-003 §4.5), the same read as <c>document-management.md</c> F-6 (record F-3).
/// </summary>
internal sealed class AuditReadRepository(PMPlatformDbContext context) : IAuditReadRepository
{
    public async Task<(IReadOnlyList<AnchoredAuditEvent> Items, int TotalCount)> PageEventsAsync(
        IReadOnlyList<AuditReach> reach, AuditEventQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reach);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        IQueryable<EventRow> rows = EventRows().Where(PredicateComposition.AnyOf(reach.Select(ReachedBy)));
        rows = Filter(rows, query);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<EventRow> items = await rows
            .OrderByDescending(r => r.Event.OccurredAt).ThenByDescending(r => r.Event.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ([.. items.Select(Anchored)], total);
    }

    public async Task<AnchoredAuditEvent?> FindEventAsync(Guid auditEventId, CancellationToken cancellationToken) =>
        await EventRows().SingleOrDefaultAsync(r => r.Event.Id == auditEventId, cancellationToken).ConfigureAwait(false) is { } row
            ? Anchored(row)
            : null;

    public async Task<IReadOnlyList<AuditEventAttribute>> ListAttributesAsync(Guid auditEventId, CancellationToken cancellationToken) =>
        await context.Set<AuditEventAttribute>().AsNoTracking().Where(a => a.AuditEventId == auditEventId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<AuditForwardingRecord?> FindForwardingAsync(Guid auditEventId, CancellationToken cancellationToken) =>
        context.Set<AuditForwardingRecord>().AsNoTracking().SingleOrDefaultAsync(f => f.AuditEventId == auditEventId, cancellationToken);

    public async Task<(IReadOnlyList<AnchoredActivityEntry> Items, int TotalCount)> PageActivityAsync(
        RecordScope scope, ActivityQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        IQueryable<ActivityRow> rows =
            from a in context.Set<BusinessActivityEntry>().AsNoTracking()
            join e in context.Set<AuditEvent>() on a.AuditEventId equals e.Id
            join p in context.Set<ProjectEntity>() on a.ProjectId equals p.Id into projects
            from p in projects.DefaultIfEmpty()
            join u in context.Set<User>() on a.ActorUserId equals u.Id into actors
            from u in actors.DefaultIfEmpty()
            select new ActivityRow
            {
                Entry = a,
                EventType = e.EventType,
                SubjectModule = e.SubjectModule,
                SubjectType = e.SubjectType,
                SubjectId = e.SubjectId,
                DataClassificationItemId = e.DataClassificationItemId,
                DepartmentId = (Guid?)p.DepartmentId,
                ProjectManagerUserId = p.ProjectManagerUserId,
                ExternalEntityId = e.ScopeExternalEntityId ?? p.ExternalEntityId,
                ActorDisplayName = u.DisplayName,
                ActorUserType = (UserType?)u.UserType,
                ActorExternalEntityId = u.ExternalEntityId,
            };

        rows = rows.Where(PredicateComposition.AnyOf(scope.Clauses.Where(c => c.AssignedUserId is null).Select(ActivityClauseOf)));
        if (query.ProjectId is { } projectId)
        {
            rows = rows.Where(r => r.Entry.ProjectId == projectId);
        }

        if (query.SubjectType is { } subjectType)
        {
            rows = rows.Where(r => r.SubjectType == subjectType);
        }

        if (query.SubjectId is { } subjectId)
        {
            rows = rows.Where(r => r.SubjectId == subjectId);
        }

        if (query.ActivityKinds.Count > 0)
        {
            List<string> kinds = [.. query.ActivityKinds];
            rows = rows.Where(r => kinds.Contains(r.Entry.ActivityKind));
        }

        if (query.ActorUserId is { } actorUserId)
        {
            rows = rows.Where(r => r.Entry.ActorUserId == actorUserId);
        }

        if (query.OccurredFrom is { } from)
        {
            rows = rows.Where(r => r.Entry.OccurredAt >= from);
        }

        if (query.OccurredTo is { } to)
        {
            rows = rows.Where(r => r.Entry.OccurredAt < to);
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ActivityRow> items = await rows
            .OrderByDescending(r => r.Entry.OccurredAt).ThenByDescending(r => r.Entry.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ([.. items.Select(r => new AnchoredActivityEntry(
            r.Entry, r.EventType, r.SubjectModule, r.SubjectType, r.SubjectId, r.ActorDisplayName, r.ActorUserType, r.ActorExternalEntityId))], total);
    }

    private IQueryable<EventRow> EventRows() =>
        from e in context.Set<AuditEvent>().AsNoTracking()
        join p in context.Set<ProjectEntity>() on e.ScopeProjectId equals p.Id into projects
        from p in projects.DefaultIfEmpty()
        join u in context.Set<User>() on e.ActorUserId equals u.Id into actors
        from u in actors.DefaultIfEmpty()
        select new EventRow
        {
            Event = e,
            DepartmentId = (Guid?)p.DepartmentId,
            ProjectManagerUserId = p.ProjectManagerUserId,
            ExternalEntityId = e.ScopeExternalEntityId ?? p.ExternalEntityId,
            ActorDisplayName = u.DisplayName,
        };

    private static IQueryable<EventRow> Filter(IQueryable<EventRow> rows, AuditEventQuery query)
    {
        if (query.OccurredFrom is { } from)
        {
            rows = rows.Where(r => r.Event.OccurredAt >= from);
        }

        if (query.OccurredTo is { } to)
        {
            rows = rows.Where(r => r.Event.OccurredAt < to);
        }

        if (query.ActorUserId is { } actorUserId)
        {
            rows = rows.Where(r => r.Event.ActorUserId == actorUserId);
        }

        if (query.EventClasses.Count > 0)
        {
            List<Domain.Common.AuditEventClass> classes = [.. query.EventClasses];
            rows = rows.Where(r => classes.Contains(r.Event.EventClass));
        }

        if (query.EventType is { } eventType)
        {
            rows = rows.Where(r => r.Event.EventType == eventType);
        }

        if (query.Outcomes.Count > 0)
        {
            List<Domain.Common.AuditOutcome> outcomes = [.. query.Outcomes];
            rows = rows.Where(r => outcomes.Contains(r.Event.Outcome));
        }

        if (query.ProjectId is { } projectId)
        {
            rows = rows.Where(r => r.Event.ScopeProjectId == projectId);
        }

        if (query.SubjectType is { } subjectType)
        {
            rows = rows.Where(r => r.Event.SubjectType == subjectType);
        }

        if (query.SubjectId is { } subjectId)
        {
            rows = rows.Where(r => r.Event.SubjectId == subjectId);
        }

        if (query.CorrelationId is { } correlationId)
        {
            rows = rows.Where(r => r.Event.CorrelationId == correlationId);
        }

        return rows;
    }

    /// <summary>One category's events that its permission's scope reaches: the category's condition AND the OR of the scope's clauses.</summary>
    private static Expression<Func<EventRow, bool>> ReachedBy(AuditReach reach) =>
        PredicateComposition.Both(
            PredicateComposition.Over(AuditCategories.In(reach.Category), (EventRow r) => r.Event),
            PredicateComposition.AnyOf(reach.Scope.Clauses.Where(c => c.AssignedUserId is null).Select(EventClauseOf)));

    /// <summary>
    /// A <see cref="RecordScopeClause"/> as SQL over an event's anchors, matching <see cref="RecordScopeClause.Matches"/>. An event
    /// has no assignees, so an ASSIGNED clause reaches none; an unclassified event passes any clearance.
    /// </summary>
    private static Expression<Func<EventRow, bool>> EventClauseOf(RecordScopeClause clause)
    {
        Guid? project = clause.ProjectId;
        Guid? entity = clause.ExternalEntityId;
        Guid? department = clause.DepartmentId;
        Guid? owner = clause.OwnerUserId;
        List<Guid> cleared = [.. clause.ClearedClassificationIds];
        return r => (project == null || r.Event.ScopeProjectId == project)
                    && (entity == null || r.ExternalEntityId == entity)
                    && (department == null || r.DepartmentId == department)
                    && (owner == null || r.ProjectManagerUserId == owner)
                    && (r.Event.DataClassificationItemId == null || cleared.Contains(r.Event.DataClassificationItemId.Value));
    }

    private static Expression<Func<ActivityRow, bool>> ActivityClauseOf(RecordScopeClause clause)
    {
        Guid? project = clause.ProjectId;
        Guid? entity = clause.ExternalEntityId;
        Guid? department = clause.DepartmentId;
        Guid? owner = clause.OwnerUserId;
        List<Guid> cleared = [.. clause.ClearedClassificationIds];
        return r => (project == null || r.Entry.ProjectId == project)
                    && (entity == null || r.ExternalEntityId == entity)
                    && (department == null || r.DepartmentId == department)
                    && (owner == null || r.ProjectManagerUserId == owner)
                    && (r.DataClassificationItemId == null || cleared.Contains(r.DataClassificationItemId.Value));
    }

    private static AnchoredAuditEvent Anchored(EventRow row) =>
        new(row.Event, row.DepartmentId, row.ProjectManagerUserId, row.ExternalEntityId, row.ActorDisplayName);

    private sealed class EventRow
    {
        public required AuditEvent Event { get; init; }

        public Guid? DepartmentId { get; init; }

        public Guid? ProjectManagerUserId { get; init; }

        public Guid? ExternalEntityId { get; init; }

        public string? ActorDisplayName { get; init; }
    }

    private sealed class ActivityRow
    {
        public required BusinessActivityEntry Entry { get; init; }

        public required string EventType { get; init; }

        public string? SubjectModule { get; init; }

        public string? SubjectType { get; init; }

        public Guid? SubjectId { get; init; }

        public Guid? DataClassificationItemId { get; init; }

        public Guid? DepartmentId { get; init; }

        public Guid? ProjectManagerUserId { get; init; }

        public Guid? ExternalEntityId { get; init; }

        public string? ActorDisplayName { get; init; }

        public UserType? ActorUserType { get; init; }

        public Guid? ActorExternalEntityId { get; init; }
    }
}
