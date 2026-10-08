using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.FinancialKpi;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.FinancialKpi;

/// <summary>The <c>financial_kpi</c> schema (TASK-052).</summary>
/// <remarks>
/// The "latest of each" reads load the projects' or assignments' rows and pick in memory: a portfolio request names at most
/// 200 (the API's limit) projects, each with one row per reporting period, so the set stays small.
/// </remarks>
internal sealed class FinancialKpiRepository(PMPlatformDbContext context) : IFinancialKpiRepository
{
    private static readonly FinancialUpdateStatus[] Reported = [FinancialUpdateStatus.Submitted, FinancialUpdateStatus.UnderReview, FinancialUpdateStatus.Published];

    public async Task<IFinancialKpiWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new FinancialKpiWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new FinancialKpiWork(null);

    public async Task<(int Unpublished, int OpenVersions)> CountUnsettledAsync(Guid projectId, CancellationToken cancellationToken)
    {
        ApprovedVersionStatus[] open = [ApprovedVersionStatus.Draft, ApprovedVersionStatus.Submitted, ApprovedVersionStatus.UnderReview, ApprovedVersionStatus.Returned];
        IQueryable<Guid> assignments = context.Set<KpiAssignment>().Where(a => a.ProjectId == projectId).Select(a => a.Id);
        int updates = await context.Set<FinancialProgressUpdate>().CountAsync(u => u.ProjectId == projectId && u.Status != FinancialUpdateStatus.Published, cancellationToken).ConfigureAwait(false);
        int measurements = await context.Set<KpiMeasurement>().CountAsync(m => assignments.Contains(m.KpiAssignmentId) && m.Status != KpiMeasurementStatus.Published, cancellationToken)
            .ConfigureAwait(false);
        int commitments = await context.Set<FinancialCommitment>().CountAsync(c => c.ProjectId == projectId && open.Contains(c.Status), cancellationToken).ConfigureAwait(false);
        int targets = await context.Set<KpiTargetVersion>().CountAsync(t => assignments.Contains(t.KpiAssignmentId) && open.Contains(t.Status), cancellationToken).ConfigureAwait(false);
        return (updates + measurements, commitments + targets);
    }

    public uint RowVersionOf(AuditedEntity entity) => context.Entry(entity).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public void Add(AuditedEntity entity) => context.Add(entity);

    public void Remove(AuditedEntity entity) => context.Remove(entity);

    public async Task<FinancialKpiSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return FinancialKpiSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return FinancialKpiSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.ChangeTracker.Clear();
            return FinancialKpiSaveOutcome.Duplicate;
        }
    }

    public async Task<IReadOnlyList<FinancialSourceMode>> ListSourceModesAsync(Guid projectId, CancellationToken cancellationToken) =>
        await context.Set<FinancialSourceMode>().AsNoTracking().Where(m => m.ProjectId == projectId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<FinancialSourceMode?> FindSourceModeAsync(Guid sourceModeId, uint? expectedVersion, CancellationToken cancellationToken) =>
        FindAsync<FinancialSourceMode>(sourceModeId, expectedVersion, cancellationToken);

    public async Task<IReadOnlyList<FinancialCommitment>> ListCommitmentsAsync(Guid projectId, CommitmentType type, CancellationToken cancellationToken) =>
        await context.Set<FinancialCommitment>().AsNoTracking().Where(c => c.ProjectId == projectId && c.CommitmentType == type).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<(IReadOnlyList<FinancialCommitment> Items, int TotalCount)> PageCommitmentsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<FinancialCommitment>().AsNoTracking().Where(c => c.ProjectId == projectId).OrderByDescending(c => c.Id), page, cancellationToken);

    public Task<FinancialCommitment?> FindCommitmentAsync(Guid commitmentId, uint? expectedVersion, CancellationToken cancellationToken) =>
        FindAsync<FinancialCommitment>(commitmentId, expectedVersion, cancellationToken);

    public Task<FinancialCommitment?> FindActiveCommitmentAsync(Guid projectId, CommitmentType type, bool track, CancellationToken cancellationToken)
    {
        IQueryable<FinancialCommitment> rows = context.Set<FinancialCommitment>();
        return (track ? rows : rows.AsNoTracking())
            .SingleOrDefaultAsync(c => c.ProjectId == projectId && c.CommitmentType == type && c.Status == ApprovedVersionStatus.Active, cancellationToken);
    }

    public async Task<IReadOnlyList<FinancialCommitment>> ListActiveBudgetsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        await context.Set<FinancialCommitment>().AsNoTracking()
            .Where(c => projectIds.Contains(c.ProjectId) && c.CommitmentType == CommitmentType.ApprovedBudget && c.Status == ApprovedVersionStatus.Active)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<(IReadOnlyList<FinancialProgressUpdate> Items, int TotalCount)> PageUpdatesAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<FinancialProgressUpdate>().AsNoTracking().Where(u => u.ProjectId == projectId).OrderByDescending(u => u.Id), page, cancellationToken);

    public Task<FinancialProgressUpdate?> FindUpdateAsync(Guid updateId, uint? expectedVersion, CancellationToken cancellationToken) =>
        FindAsync<FinancialProgressUpdate>(updateId, expectedVersion, cancellationToken);

    public async Task<IReadOnlyList<FinancialProgressUpdate>> ListUpdatesAsync(Guid projectId, CancellationToken cancellationToken) =>
        await context.Set<FinancialProgressUpdate>().AsNoTracking().Where(u => u.ProjectId == projectId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<FinancialProgressUpdate>> ListLatestReportedUpdatesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        [.. (await context.Set<FinancialProgressUpdate>().AsNoTracking()
                .Where(u => projectIds.Contains(u.ProjectId) && Reported.Contains(u.Status))
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .GroupBy(u => u.ProjectId)
            .Select(g => g.MaxBy(u => (u.SubmittedAt, u.Id))!)];

    public Task<(IReadOnlyList<PublishedFinancialSnapshot> Items, int TotalCount)> PageSnapshotsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(
            context.Set<PublishedFinancialSnapshot>().AsNoTracking().Where(s => s.ProjectId == projectId).OrderByDescending(s => s.PublishedAt).ThenByDescending(s => s.Id),
            page, cancellationToken);

    public async Task<IReadOnlyList<PublishedFinancialSnapshot>> ListLatestSnapshotsAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        [.. (await context.Set<PublishedFinancialSnapshot>().AsNoTracking().Where(s => projectIds.Contains(s.ProjectId)).ToListAsync(cancellationToken).ConfigureAwait(false))
            .GroupBy(s => s.ProjectId)
            .Select(g => g.MaxBy(s => (s.PublishedAt, s.Id))!)];

    public Task<(IReadOnlyList<KpiAssignment> Items, int TotalCount)> PageAssignmentsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<KpiAssignment>().AsNoTracking().Where(a => a.ProjectId == projectId).OrderByDescending(a => a.Id), page, cancellationToken);

    public Task<KpiAssignment?> FindAssignmentAsync(Guid assignmentId, uint? expectedVersion, CancellationToken cancellationToken) =>
        FindAsync<KpiAssignment>(assignmentId, expectedVersion, cancellationToken);

    public async Task<IReadOnlyList<KpiAssignment>> ListAssignmentsAsync(
        IReadOnlyCollection<Guid> projectIds, IReadOnlyCollection<Guid> kpiDefinitionIds, CancellationToken cancellationToken) =>
        await context.Set<KpiAssignment>().AsNoTracking()
            .Where(a => projectIds.Contains(a.ProjectId) && kpiDefinitionIds.Contains(a.KpiDefinitionId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<(IReadOnlyList<KpiTargetVersion> Items, int TotalCount)> PageTargetsAsync(Guid assignmentId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<KpiTargetVersion>().AsNoTracking().Where(t => t.KpiAssignmentId == assignmentId).OrderByDescending(t => t.VersionNo), page, cancellationToken);

    public async Task<IReadOnlyList<KpiTargetVersion>> ListTargetsAsync(Guid assignmentId, CancellationToken cancellationToken) =>
        await context.Set<KpiTargetVersion>().AsNoTracking().Where(t => t.KpiAssignmentId == assignmentId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<KpiTargetVersion?> FindTargetAsync(Guid targetVersionId, uint? expectedVersion, CancellationToken cancellationToken) =>
        FindAsync<KpiTargetVersion>(targetVersionId, expectedVersion, cancellationToken);

    public Task<KpiTargetVersion?> FindActiveTargetAsync(Guid assignmentId, bool track, CancellationToken cancellationToken)
    {
        IQueryable<KpiTargetVersion> rows = context.Set<KpiTargetVersion>();
        return (track ? rows : rows.AsNoTracking()).SingleOrDefaultAsync(t => t.KpiAssignmentId == assignmentId && t.Status == ApprovedVersionStatus.Active, cancellationToken);
    }

    public async Task<IReadOnlyList<KpiTargetVersion>> ListTargetsByIdAsync(IReadOnlyCollection<Guid> targetVersionIds, CancellationToken cancellationToken) =>
        await context.Set<KpiTargetVersion>().AsNoTracking().Where(t => targetVersionIds.Contains(t.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<(IReadOnlyList<KpiMeasurement> Items, int TotalCount)> PageMeasurementsAsync(Guid assignmentId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(
            context.Set<KpiMeasurement>().AsNoTracking().Where(m => m.KpiAssignmentId == assignmentId).OrderByDescending(m => m.PeriodStart),
            page, cancellationToken);

    public Task<KpiMeasurement?> FindMeasurementAsync(Guid measurementId, uint? expectedVersion, CancellationToken cancellationToken) =>
        FindAsync<KpiMeasurement>(measurementId, expectedVersion, cancellationToken);

    public async Task<IReadOnlyList<KpiMeasurement>> ListLatestPublishedMeasurementsAsync(IReadOnlyCollection<Guid> assignmentIds, CancellationToken cancellationToken) =>
        [.. (await context.Set<KpiMeasurement>().AsNoTracking()
                .Where(m => assignmentIds.Contains(m.KpiAssignmentId) && m.Status == KpiMeasurementStatus.Published)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .GroupBy(m => m.KpiAssignmentId)
            .Select(g => g.MaxBy(m => m.PeriodStart)!)];

    private async Task<TEntity?> FindAsync<TEntity>(Guid id, uint? expectedVersion, CancellationToken cancellationToken)
        where TEntity : AuditedEntity
    {
        TEntity? entity = await context.Set<TEntity>().SingleOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is not null)
        {
            AdministrationPersistence.ExpectVersion(context, entity, expectedVersion);
        }

        return entity;
    }

    private static async Task<(IReadOnlyList<TEntity> Items, int TotalCount)> PageAsync<TEntity>(IOrderedQueryable<TEntity> rows, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<TEntity> items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    /// <summary>Owns its transaction when it began one; joined to the caller's otherwise, which the caller commits.</summary>
    private sealed class FinancialKpiWork(IDbContextTransaction? transaction) : IFinancialKpiWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
