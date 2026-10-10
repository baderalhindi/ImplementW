using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Reports;
using PMPlatform.Application.Features.Reports.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Reports;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Reports;

/// <summary>The <c>reports</c> schema (TASK-071).</summary>
internal sealed class ReportRepository(PMPlatformDbContext context) : IReportRepository
{
    public async Task<IReadOnlyList<ReportDefinition>> ListPublishedAsync(CancellationToken cancellationToken) =>
        await Rows<ReportDefinition>(track: false).Where(d => d.LifecycleState == GovernedLifecycleState.Published).OrderBy(d => d.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<ReportDefinition?> FindPublishedAsync(ReportCode code, bool track, CancellationToken cancellationToken) =>
        Rows<ReportDefinition>(track).SingleOrDefaultAsync(d => d.Code == code && d.LifecycleState == GovernedLifecycleState.Published, cancellationToken);

    public async Task<(IReadOnlyList<ReportDefinition> Items, int TotalCount)> PageDefinitionsAsync(ReportDefinitionQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        IQueryable<ReportDefinition> rows = Rows<ReportDefinition>(track: false);
        if (query.Code is { } code)
        {
            rows = rows.Where(d => d.Code == code);
        }

        if (query.LifecycleState is { } state)
        {
            rows = rows.Where(d => d.LifecycleState == state);
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ReportDefinition> items = await rows.OrderBy(d => d.Code).ThenByDescending(d => d.VersionNo)
            .Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<ReportDefinition?> FindDefinitionAsync(Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ReportDefinition? definition = await Rows<ReportDefinition>(track: true).SingleOrDefaultAsync(d => d.Id == definitionId, cancellationToken).ConfigureAwait(false);
        if (definition is not null)
        {
            AdministrationPersistence.ExpectVersion(context, definition, expectedVersion);
        }

        return definition;
    }

    public Task<ReportDefinition?> ReadDefinitionAsync(Guid definitionId, CancellationToken cancellationToken) =>
        Rows<ReportDefinition>(track: false).SingleOrDefaultAsync(d => d.Id == definitionId, cancellationToken);

    public Task<ReportDefinition?> FindOpenAsync(ReportCode code, CancellationToken cancellationToken) =>
        Rows<ReportDefinition>(track: false).SingleOrDefaultAsync(
            d => d.Code == code && (d.LifecycleState == GovernedLifecycleState.Draft || d.LifecycleState == GovernedLifecycleState.Validated), cancellationToken);

    public async Task<int> MaxVersionNoAsync(ReportCode code, CancellationToken cancellationToken) =>
        await Rows<ReportDefinition>(track: false).Where(d => d.Code == code).MaxAsync(d => (int?)d.VersionNo, cancellationToken).ConfigureAwait(false) ?? 0;

    public async Task<IReadOnlyList<ReportColumn>> ListColumnsAsync(Guid definitionId, bool track, CancellationToken cancellationToken) =>
        await Rows<ReportColumn>(track).Where(c => c.ReportDefinitionId == definitionId).OrderBy(c => c.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportAudienceRole>> ListAudienceAsync(IReadOnlyCollection<Guid> definitionIds, bool track, CancellationToken cancellationToken) =>
        await Rows<ReportAudienceRole>(track).Where(a => definitionIds.Contains(a.ReportDefinitionId)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportParameter>> ListParametersAsync(Guid definitionId, bool track, CancellationToken cancellationToken) =>
        await Rows<ReportParameter>(track).Where(p => p.ReportDefinitionId == definitionId).OrderBy(p => p.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportParameterOption>> ListOptionsAsync(IReadOnlyCollection<Guid> parameterIds, bool track, CancellationToken cancellationToken) =>
        parameterIds.Count == 0
            ? []
            : await Rows<ReportParameterOption>(track).Where(o => parameterIds.Contains(o.ReportParameterId)).OrderBy(o => o.ValueCode)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<SavedView> Items, int TotalCount)> PageViewsAsync(Guid ownerUserId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        IQueryable<SavedView> rows = Rows<SavedView>(track: false).Where(v => v.OwnerUserId == ownerUserId);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<SavedView> items = await rows.OrderByDescending(v => v.UpdatedAt).ThenBy(v => v.Id).Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<SavedView?> FindViewAsync(Guid savedViewId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        SavedView? view = await Rows<SavedView>(track: true).SingleOrDefaultAsync(v => v.Id == savedViewId, cancellationToken).ConfigureAwait(false);
        if (view is not null)
        {
            AdministrationPersistence.ExpectVersion(context, view, expectedVersion);
        }

        return view;
    }

    public async Task<IReadOnlyList<SavedViewColumn>> ListViewColumnsAsync(Guid savedViewId, bool track, CancellationToken cancellationToken) =>
        await Rows<SavedViewColumn>(track).Where(c => c.SavedViewId == savedViewId).OrderBy(c => c.SortOrder).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<SavedViewFilter>> ListViewFiltersAsync(Guid savedViewId, bool track, CancellationToken cancellationToken) =>
        await Rows<SavedViewFilter>(track).Where(f => f.SavedViewId == savedViewId).OrderBy(f => f.CreatedAt).ThenBy(f => f.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ReportParameterValue>> ListViewParametersAsync(Guid savedViewId, bool track, CancellationToken cancellationToken) =>
        await Rows<ReportParameterValue>(track).Where(p => p.SavedViewId == savedViewId).OrderBy(p => p.ParameterCode).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<ReportJob?> FindJobByKeyAsync(Guid requestedByUserId, Guid idempotencyKey, CancellationToken cancellationToken) =>
        Rows<ReportJob>(track: false).SingleOrDefaultAsync(j => j.RequestedByUserId == requestedByUserId && j.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<(IReadOnlyList<ReportJob> Items, int TotalCount)> PageJobsAsync(Guid requestedByUserId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        IQueryable<ReportJob> rows = Rows<ReportJob>(track: false).Where(j => j.RequestedByUserId == requestedByUserId);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ReportJob> items = await rows.OrderByDescending(j => j.RequestedAt).ThenByDescending(j => j.Id).Skip(page.Skip).Take(page.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<ReportJob?> FindJobAsync(Guid jobId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ReportJob? job = await Rows<ReportJob>(track: true).SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken).ConfigureAwait(false);
        if (job is not null)
        {
            AdministrationPersistence.ExpectVersion(context, job, expectedVersion);
        }

        return job;
    }

    public async Task<IReadOnlyList<Guid>> ListJobIdsAsync(ReportJobStatus status, int batchSize, CancellationToken cancellationToken) =>
        await Rows<ReportJob>(track: false).Where(j => j.Status == status).OrderBy(j => j.RequestedAt).ThenBy(j => j.Id).Take(batchSize).Select(j => j.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> ListAbandonedJobIdsAsync(DateTimeOffset before, int batchSize, CancellationToken cancellationToken) =>
        await Rows<ReportJob>(track: false)
            .Where(j => (j.Status == ReportJobStatus.Validating || j.Status == ReportJobStatus.Running) && j.UpdatedAt < before)
            .OrderBy(j => j.UpdatedAt).Take(batchSize).Select(j => j.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> ListExpiredJobIdsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
        await Rows<GeneratedOutput>(track: false)
            .Where(o => (o.Status == GeneratedOutputStatus.Available || o.Status == GeneratedOutputStatus.Expired) && o.ExpiresAt <= now)
            .OrderBy(o => o.ExpiresAt).Take(batchSize).Select(o => o.ReportJobId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, GeneratedOutput>> ListOutputsAsync(IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken) =>
        jobIds.Count == 0
            ? []
            : await Rows<GeneratedOutput>(track: false).Where(o => jobIds.Contains(o.ReportJobId)).ToDictionaryAsync(o => o.ReportJobId, cancellationToken).ConfigureAwait(false);

    public Task<GeneratedOutput?> FindOutputAsync(Guid jobId, CancellationToken cancellationToken) =>
        Rows<GeneratedOutput>(track: true).SingleOrDefaultAsync(o => o.ReportJobId == jobId, cancellationToken);

    public async Task<byte[]?> ReadContentAsync(string storageObjectKey, CancellationToken cancellationToken) =>
        await Rows<ReportOutputContent>(track: false).Where(c => c.StorageObjectKey == storageObjectKey).Select(c => c.Content)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    public async Task RemoveContentAsync(string storageObjectKey, CancellationToken cancellationToken)
    {
        if (await Rows<ReportOutputContent>(track: true).SingleOrDefaultAsync(c => c.StorageObjectKey == storageObjectKey, cancellationToken).ConfigureAwait(false) is { } content)
        {
            context.Remove(content);
        }
    }

    public uint RowVersionOf(AuditedEntity row) => context.Entry(row).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public void Add(AuditedEntity row) => context.Add(row);

    public void Remove(AuditedEntity row) => context.Remove(row);

    public async Task<ReportSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ReportSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return ReportSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A version number, the version on its way or a job's key taken by another request first, or — at commit — a second PUBLISHED
            // version of a code (TASK-071 migration 3).
            context.ChangeTracker.Clear();
            return ReportSaveOutcome.Duplicate;
        }
    }

    public void Clear() => context.ChangeTracker.Clear();

    private IQueryable<T> Rows<T>(bool track)
        where T : class =>
        track ? context.Set<T>() : context.Set<T>().AsNoTracking();
}
