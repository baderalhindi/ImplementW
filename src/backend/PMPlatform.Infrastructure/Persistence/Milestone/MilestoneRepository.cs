using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Milestone;
using PMPlatform.Domain.Milestone;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Milestone;

/// <summary>The <c>milestone</c> schema (TASK-050).</summary>
internal sealed class MilestoneRepository(PMPlatformDbContext context) : IMilestoneRepository
{
    public async Task<IMilestoneWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new MilestoneWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new MilestoneWork(null);

    public async Task<IReadOnlyList<MilestoneAchievement>> ListRevisionsAsync(Guid projectMilestoneId, CancellationToken cancellationToken) =>
        await context.Set<MilestoneAchievement>().AsNoTracking().Where(a => a.ProjectMilestoneId == projectMilestoneId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<(IReadOnlyList<MilestoneAchievement> Items, int TotalCount)> PageAsync(
        Guid? projectMilestoneId, Guid? projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        IQueryable<MilestoneAchievement> rows = context.Set<MilestoneAchievement>().AsNoTracking();
        rows = projectMilestoneId is { } milestoneId ? rows.Where(a => a.ProjectMilestoneId == milestoneId)
            : projectId is { } project ? rows.Where(a => a.ProjectId == project)
            : throw new ArgumentException("A page is of one milestone or of one project.", nameof(projectId));

        // Identifiers are UUIDv7, so the newest revision comes first.
        IOrderedQueryable<MilestoneAchievement> ordered = rows.OrderByDescending(a => a.Id);
        int total = await ordered.CountAsync(cancellationToken).ConfigureAwait(false);
        List<MilestoneAchievement> items = await ordered.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    public async Task<MilestoneAchievement?> FindAsync(Guid achievementId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        MilestoneAchievement? achievement = await context.Set<MilestoneAchievement>().SingleOrDefaultAsync(a => a.Id == achievementId, cancellationToken).ConfigureAwait(false);
        if (achievement is not null)
        {
            AdministrationPersistence.ExpectVersion(context, achievement, expectedVersion);
        }

        return achievement;
    }

    public Task<MilestoneAchievement?> FindAcceptedAsync(Guid projectMilestoneId, CancellationToken cancellationToken) =>
        context.Set<MilestoneAchievement>()
            .SingleOrDefaultAsync(a => a.ProjectMilestoneId == projectMilestoneId && a.Status == MilestoneAchievementStatus.Accepted, cancellationToken);

    public uint RowVersionOf(MilestoneAchievement achievement) =>
        context.Entry(achievement).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public void Add(MilestoneAchievement achievement) => context.Add(achievement);

    public void Remove(MilestoneAchievement achievement) => context.Remove(achievement);

    public async Task<MilestoneSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MilestoneSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return MilestoneSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The milestone's next revision, its one open revision or its one ACCEPTED revision, taken by another request first.
            context.ChangeTracker.Clear();
            return MilestoneSaveOutcome.Duplicate;
        }
    }

    /// <summary>Owns its transaction when it began one; joined to the caller's otherwise, which the caller commits.</summary>
    private sealed class MilestoneWork(IDbContextTransaction? transaction) : IMilestoneWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
