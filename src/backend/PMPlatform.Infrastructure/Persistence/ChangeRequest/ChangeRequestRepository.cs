using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.ChangeRequest;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Infrastructure.Persistence.ChangeRequest;

/// <summary>The <c>change_request</c> schema (TASK-060).</summary>
internal sealed class ChangeRequestRepository(PMPlatformDbContext context) : IChangeRequestRepository
{
    /// <summary>The states of a request whose change was approved: its impact counts towards the cumulative position (ADR-016).</summary>
    private static readonly ChangeRequestStatus[] Approved =
        [ChangeRequestStatus.Approved, ChangeRequestStatus.Implementation, ChangeRequestStatus.Implemented, ChangeRequestStatus.Closed];

    public async Task<IChangeRequestWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new ChangeRequestWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new ChangeRequestWork(null);

    public async Task<ChangeRequestEntity?> FindAsync(Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ChangeRequestEntity? request = await context.Set<ChangeRequestEntity>().SingleOrDefaultAsync(r => r.Id == changeRequestId, cancellationToken).ConfigureAwait(false);
        if (request is not null)
        {
            AdministrationPersistence.ExpectVersion(context, request, expectedVersion);
        }

        return request;
    }

    public Task<(IReadOnlyList<ChangeRequestEntity> Items, int TotalCount)> PageAsync(ChangeRequestQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<ChangeRequestEntity> rows = context.Set<ChangeRequestEntity>().AsNoTracking().Where(r => r.ProjectId == query.ProjectId);
        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(r => query.Statuses.Contains(r.Status));
        }

        if (query.ChangeTypes.Count > 0)
        {
            rows = rows.Where(r => query.ChangeTypes.Contains(r.ChangeType));
        }

        // indexing-strategy.md P-2: updated_at DESC, id DESC, served by I-26.
        return PageAsync(rows.OrderByDescending(r => r.UpdatedAt).ThenByDescending(r => r.Id), page, cancellationToken);
    }

    public async Task<IReadOnlyList<MaterialityEvaluation>> ListLatestEvaluationsAsync(IReadOnlyCollection<Guid> changeRequestIds, CancellationToken cancellationToken) =>
        await context.Set<MaterialityEvaluation>().AsNoTracking()
            .Where(e => changeRequestIds.Contains(e.ChangeRequestId)
                        && e.RevisionNo == context.Set<MaterialityEvaluation>().Where(l => l.ChangeRequestId == e.ChangeRequestId).Max(l => l.RevisionNo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<(Money? CostImpactSar, int? ScheduleImpactDays)>> ListApprovedImpactsAsync(
        Guid projectId, Guid? projectBaselineId, Guid excludingId, CancellationToken cancellationToken)
    {
        // An approved request's latest evaluation is the one of the revision approved: the baseline its change was evaluated against.
        var rows = await context.Set<ChangeRequestEntity>().AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.Id != excludingId && Approved.Contains(r.Status))
            .Where(r => context.Set<MaterialityEvaluation>()
                .Where(e => e.ChangeRequestId == r.Id)
                .OrderByDescending(e => e.RevisionNo)
                .Select(e => e.ProjectBaselineId)
                .FirstOrDefault() == projectBaselineId)
            .Select(r => new { r.CostImpactSar, r.ScheduleImpactDays })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => (r.CostImpactSar, r.ScheduleImpactDays))];
    }

    public async Task<IReadOnlyList<ChangeAuthorization>> ListAuthorizationsAsync(IReadOnlyCollection<Guid> changeRequestIds, CancellationToken cancellationToken) =>
        await context.Set<ChangeAuthorization>().AsNoTracking()
            .Where(a => changeRequestIds.Contains(a.ChangeRequestId))
            .OrderBy(a => a.IssuedAt).ThenBy(a => a.AuthorizationScope).ThenBy(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ChangeAuthorization?> FindAuthorizationAsync(Guid changeAuthorizationId, bool track, CancellationToken cancellationToken) =>
        (track ? context.Set<ChangeAuthorization>() : context.Set<ChangeAuthorization>().AsNoTracking()).SingleOrDefaultAsync(a => a.Id == changeAuthorizationId, cancellationToken);

    public Task<(IReadOnlyList<ChangeAuthorization> Items, int TotalCount)> PageAuthorizationsAsync(
        ChangeAuthorizationQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<ChangeAuthorization> rows = context.Set<ChangeAuthorization>().AsNoTracking()
            .Where(a => context.Set<ChangeRequestEntity>().Any(r => r.Id == a.ChangeRequestId && r.ProjectId == query.ProjectId));
        if (query.ChangeRequestId is { } changeRequestId)
        {
            rows = rows.Where(a => a.ChangeRequestId == changeRequestId);
        }

        if (query.Scopes.Count > 0)
        {
            rows = rows.Where(a => query.Scopes.Contains(a.AuthorizationScope));
        }

        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(a => query.Statuses.Contains(a.Status));
        }

        return PageAsync(rows.OrderByDescending(a => a.IssuedAt).ThenByDescending(a => a.Id), page, cancellationToken);
    }

    public uint RowVersionOf(ChangeRequestEntity changeRequest) =>
        context.Entry(changeRequest).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public void Add(ChangeRequestEntity changeRequest) => context.Add(changeRequest);

    public void Add(MaterialityEvaluation evaluation) => context.Add(evaluation);

    public void Add(ChangeAuthorization authorization) => context.Add(authorization);

    public void Remove(ChangeRequestEntity changeRequest) => context.Remove(changeRequest);

    public async Task<ChangeRequestSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ChangeRequestSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return ChangeRequestSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A revision's evaluation, or an authorisation's issuance key, taken by another request first.
            context.ChangeTracker.Clear();
            return ChangeRequestSaveOutcome.Duplicate;
        }
    }

    private static async Task<(IReadOnlyList<T> Items, int TotalCount)> PageAsync<T>(IQueryable<T> rows, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<T> items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    /// <summary>Owns its transaction when it began one; joined to the caller's otherwise — the outbox dispatch's, for an approval's outcome.</summary>
    private sealed class ChangeRequestWork(IDbContextTransaction? transaction) : IChangeRequestWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
