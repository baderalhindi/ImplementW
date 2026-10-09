using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ExternalParticipation;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.ExternalParticipation;

/// <summary>The <c>external_participation</c> schema (TASK-066).</summary>
internal sealed class ExternalParticipationRepository(PMPlatformDbContext context) : IExternalParticipationRepository
{
    public async Task<IExternalParticipationWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new ExternalParticipationWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new ExternalParticipationWork(null);

    public async Task<ExternalUpdateRequest?> FindRequestAsync(Guid requestId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ExternalUpdateRequest? request = await context.Set<ExternalUpdateRequest>().SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (request is not null)
        {
            AdministrationPersistence.ExpectVersion(context, request, expectedVersion);
        }

        return request;
    }

    public async Task<(IReadOnlyList<ExternalUpdateRequest> Items, int TotalCount)> PageRequestsAsync(
        RecordScope scope, ExternalUpdateRequestQuery query, bool issuedOnly, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        IQueryable<AnchoredRequest> rows =
            from r in context.Set<ExternalUpdateRequest>().AsNoTracking()
            join p in context.Set<ProjectEntity>() on r.ProjectId equals p.Id
            select new AnchoredRequest { Request = r, DepartmentId = p.DepartmentId, ProjectManagerUserId = p.ProjectManagerUserId };

        rows = rows.Where(Reached(scope));
        if (issuedOnly)
        {
            rows = rows.Where(a => a.Request.Status != ExternalUpdateRequestStatus.Draft);
        }

        if (query.ProjectId is { } projectId)
        {
            rows = rows.Where(a => a.Request.ProjectId == projectId);
        }

        if (query.ExternalEntityId is { } entityId)
        {
            rows = rows.Where(a => a.Request.ExternalEntityId == entityId);
        }

        if (query.Statuses.Count > 0)
        {
            List<ExternalUpdateRequestStatus> statuses = [.. query.Statuses];
            rows = rows.Where(a => statuses.Contains(a.Request.Status));
        }

        if (query.ResponsibleUserId is { } responder)
        {
            rows = rows.Where(a => a.Request.ResponsibleUserId == responder);
        }

        if (query.ReviewerUserId is { } reviewer)
        {
            rows = rows.Where(a => a.Request.ReviewerUserId == reviewer);
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ExternalUpdateRequest> items = await rows
            .OrderByDescending(a => a.Request.UpdatedAt).ThenByDescending(a => a.Request.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(a => a.Request)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public async Task<ExternalContribution?> FindContributionAsync(Guid contributionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ExternalContribution? contribution = await context.Set<ExternalContribution>().SingleOrDefaultAsync(c => c.Id == contributionId, cancellationToken).ConfigureAwait(false);
        if (contribution is not null)
        {
            AdministrationPersistence.ExpectVersion(context, contribution, expectedVersion);
        }

        return contribution;
    }

    public Task<(IReadOnlyList<ExternalContribution> Items, int TotalCount)> PageRevisionsAsync(Guid requestId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<ExternalContribution>().AsNoTracking().Where(c => c.ExternalUpdateRequestId == requestId).OrderByDescending(c => c.RevisionNo), page, cancellationToken);

    public async Task<IReadOnlyList<ExternalContributionField>> ListFieldsAsync(IReadOnlyCollection<Guid> contributionIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = [.. contributionIds];
        return await context.Set<ExternalContributionField>().AsNoTracking()
            .Where(f => ids.Contains(f.ExternalContributionId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ExternalContributionField>> FindFieldsAsync(Guid contributionId, CancellationToken cancellationToken) =>
        await context.Set<ExternalContributionField>().Where(f => f.ExternalContributionId == contributionId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<SourceApplication?> FindApplicationAsync(Guid applicationId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        SourceApplication? application = await context.Set<SourceApplication>().SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken).ConfigureAwait(false);
        if (application is not null)
        {
            AdministrationPersistence.ExpectVersion(context, application, expectedVersion);
        }

        return application;
    }

    public Task<SourceApplication?> FindApplicationByKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        context.Set<SourceApplication>().AsNoTracking().SingleOrDefaultAsync(a => a.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<SourceApplication>> ListApplicationsAsync(Guid contributionId, CancellationToken cancellationToken) =>
        await context.Set<SourceApplication>().AsNoTracking()
            .Where(a => a.ExternalContributionId == contributionId)
            .OrderBy(a => a.AttemptNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<(IReadOnlyList<SourceApplication> Items, int TotalCount)> PageApplicationsAsync(Guid contributionId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<SourceApplication>().AsNoTracking().Where(a => a.ExternalContributionId == contributionId).OrderByDescending(a => a.AttemptNo), page, cancellationToken);

    public uint RowVersionOf(ExternalUpdateRequest request) => RowVersion(request);

    public uint RowVersionOf(ExternalContribution contribution) => RowVersion(contribution);

    public uint RowVersionOf(SourceApplication application) => RowVersion(application);

    public void Add(ExternalUpdateRequest request) => context.Add(request);

    public void Add(ExternalContribution contribution) => context.Add(contribution);

    public void Add(ExternalContributionField field) => context.Add(field);

    public void Add(SourceApplication application) => context.Add(application);

    public void Remove(ExternalUpdateRequest request) => context.Remove(request);

    public void Remove(ExternalContributionField field) => context.Remove(field);

    public async Task<ExternalParticipationSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ExternalParticipationSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return ExternalParticipationSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A revision number, an attempt number, an Idempotency-Key, or the revision's one applied attempt, taken by another request first.
            context.ChangeTracker.Clear();
            return ExternalParticipationSaveOutcome.Duplicate;
        }
    }

    /// <summary>
    /// <see cref="RecordScope"/> as SQL: the OR of its clauses, each the AND of its conditions on the request's anchors — its own entity
    /// and project, its project's department and Project Manager, and its responder and reviewer as the people assigned to it. Requests
    /// are not classified.
    /// </summary>
    private static Expression<Func<AnchoredRequest, bool>> Reached(RecordScope scope) => PredicateComposition.AnyOf(scope.Clauses.Select(ClauseOf));

    private static Expression<Func<AnchoredRequest, bool>> ClauseOf(RecordScopeClause clause)
    {
        Guid? project = clause.ProjectId;
        Guid? entity = clause.ExternalEntityId;
        Guid? department = clause.DepartmentId;
        Guid? owner = clause.OwnerUserId;
        Guid? assignee = clause.AssignedUserId;
        return a => (project == null || a.Request.ProjectId == project)
                    && (entity == null || a.Request.ExternalEntityId == entity)
                    && (department == null || a.DepartmentId == department)
                    && (owner == null || a.ProjectManagerUserId == owner)
                    && (assignee == null || a.Request.ResponsibleUserId == assignee || a.Request.ReviewerUserId == assignee);
    }

    private uint RowVersion<TRow>(TRow row)
        where TRow : class => context.Entry(row).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    private static async Task<(IReadOnlyList<T> Items, int TotalCount)> PageAsync<T>(IQueryable<T> rows, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<T> items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    /// <summary>A request with its project's department and Project Manager, the anchors its scope is decided on beside its own.</summary>
    private sealed class AnchoredRequest
    {
        public required ExternalUpdateRequest Request { get; init; }

        public Guid DepartmentId { get; init; }

        public Guid? ProjectManagerUserId { get; init; }
    }

    /// <summary>Owns its transaction when it began one; joined to the caller's otherwise.</summary>
    private sealed class ExternalParticipationWork(IDbContextTransaction? transaction) : IExternalParticipationWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
