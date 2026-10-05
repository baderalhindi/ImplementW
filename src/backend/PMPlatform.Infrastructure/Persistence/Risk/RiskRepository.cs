using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Risk;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Risk;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Infrastructure.Persistence.Risk;

/// <summary>The <c>risk</c> schema (TASK-055).</summary>
internal sealed class RiskRepository(PMPlatformDbContext context) : IRiskRepository
{
    public async Task<IRiskWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new RiskWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new RiskWork(null);

    public async Task<RiskEntity?> FindRiskAsync(Guid riskId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        RiskEntity? risk = await context.Set<RiskEntity>().SingleOrDefaultAsync(r => r.Id == riskId, cancellationToken).ConfigureAwait(false);
        if (risk is not null)
        {
            AdministrationPersistence.ExpectVersion(context, risk, expectedVersion);
        }

        return risk;
    }

    public Task<(IReadOnlyList<RiskEntity> Items, int TotalCount)> PageRisksAsync(RiskQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<RiskEntity> rows = context.Set<RiskEntity>().AsNoTracking().Where(r => r.ProjectId == query.ProjectId);
        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(r => query.Statuses.Contains(r.Status));
        }

        if (query.OwnerUserId is { } owner)
        {
            rows = rows.Where(r => r.OwnerUserId == owner);
        }

        if (query.NextReviewDateFrom is { } from)
        {
            rows = rows.Where(r => r.NextReviewDate >= from);
        }

        if (query.NextReviewDateTo is { } to)
        {
            rows = rows.Where(r => r.NextReviewDate <= to);
        }

        // indexing-strategy.md P-2: updated_at DESC, id DESC, served by I-11.
        return PageAsync(rows.OrderByDescending(r => r.UpdatedAt).ThenByDescending(r => r.Id), page, cancellationToken);
    }

    public async Task<RiskStatus?> FindStatusAsync(Guid riskId, CancellationToken cancellationToken) =>
        await context.Set<RiskEntity>().AsNoTracking().Where(r => r.Id == riskId).Select(r => (RiskStatus?)r.Status)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<RiskAssessmentVersion>> ListLatestAssessmentsAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
        await context.Set<RiskAssessmentVersion>().AsNoTracking()
            .Where(a => riskIds.Contains(a.RiskId)
                        && a.VersionNo == context.Set<RiskAssessmentVersion>().Where(b => b.RiskId == a.RiskId).Max(b => b.VersionNo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<(IReadOnlyList<RiskAssessmentVersion> Items, int TotalCount)> PageAssessmentsAsync(Guid riskId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<RiskAssessmentVersion>().AsNoTracking().Where(a => a.RiskId == riskId).OrderByDescending(a => a.VersionNo), page, cancellationToken);

    public Task<RiskAssessmentVersion?> FindAssessmentAsync(Guid assessmentId, CancellationToken cancellationToken) =>
        context.Set<RiskAssessmentVersion>().AsNoTracking().SingleOrDefaultAsync(a => a.Id == assessmentId, cancellationToken);

    public async Task<IReadOnlyList<RiskAssessmentImpact>> ListImpactsAsync(IReadOnlyCollection<Guid> assessmentIds, CancellationToken cancellationToken) =>
        await context.Set<RiskAssessmentImpact>().AsNoTracking().Where(i => assessmentIds.Contains(i.RiskAssessmentVersionId)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<RiskAcceptance>> ListActiveAcceptancesAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
        await context.Set<RiskAcceptance>().AsNoTracking()
            .Where(a => riskIds.Contains(a.RiskId) && a.Status == RiskAcceptanceStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<RiskAcceptance?> FindActiveAcceptanceAsync(Guid riskId, CancellationToken cancellationToken) =>
        context.Set<RiskAcceptance>().SingleOrDefaultAsync(a => a.RiskId == riskId && a.Status == RiskAcceptanceStatus.Active, cancellationToken);

    public Task<(IReadOnlyList<RiskAcceptance> Items, int TotalCount)> PageAcceptancesAsync(Guid riskId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<RiskAcceptance>().AsNoTracking().Where(a => a.RiskId == riskId).OrderByDescending(a => a.AcceptedAt).ThenByDescending(a => a.Id), page, cancellationToken);

    public async Task<IReadOnlyList<RiskAcceptance>> ListLapsedAcceptancesAsync(DateOnly today, int batchSize, CancellationToken cancellationToken) =>
        await context.Set<RiskAcceptance>().AsNoTracking()
            .Where(a => a.Status == RiskAcceptanceStatus.Active && a.ExpiresOn <= today)
            .OrderBy(a => a.ExpiresOn).ThenBy(a => a.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<bool> HasOpenActionAsync(Guid riskId, CancellationToken cancellationToken) =>
        context.Set<RiskTreatmentAction>().AnyAsync(
            a => a.RiskId == riskId && (a.Status == RiskTreatmentActionStatus.Planned || a.Status == RiskTreatmentActionStatus.InProgress), cancellationToken);

    public Task<(IReadOnlyList<RiskTreatmentAction> Items, int TotalCount)> PageActionsAsync(Guid riskId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(context.Set<RiskTreatmentAction>().AsNoTracking().Where(a => a.RiskId == riskId).OrderBy(a => a.Id), page, cancellationToken);

    public async Task<RiskTreatmentAction?> FindActionAsync(Guid actionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        RiskTreatmentAction? action = await context.Set<RiskTreatmentAction>().SingleOrDefaultAsync(a => a.Id == actionId, cancellationToken).ConfigureAwait(false);
        if (action is not null)
        {
            AdministrationPersistence.ExpectVersion(context, action, expectedVersion);
        }

        return action;
    }

    public uint RowVersionOf(RiskEntity risk) => RowVersion(risk);

    public uint RowVersionOf(RiskTreatmentAction action) => RowVersion(action);

    public void Add(RiskEntity risk) => context.Add(risk);

    public void Add(RiskAssessmentVersion assessment) => context.Add(assessment);

    public void Add(RiskAssessmentImpact impact) => context.Add(impact);

    public void Add(RiskTreatmentAction action) => context.Add(action);

    public void Add(RiskAcceptance acceptance) => context.Add(acceptance);

    public async Task<RiskSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return RiskSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return RiskSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // An assessment's version number, or the risk's one ACTIVE acceptance, another request wrote first.
            context.ChangeTracker.Clear();
            return RiskSaveOutcome.Duplicate;
        }
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

    /// <summary>Owns its transaction when it began one; joined to the caller's otherwise, which the caller commits.</summary>
    private sealed class RiskWork(IDbContextTransaction? transaction) : IRiskWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
