using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Progress;
using PMPlatform.Domain.Progress;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Progress;

/// <summary>The <c>progress</c> schema (TASK-044).</summary>
internal sealed class ProgressRepository(PMPlatformDbContext context) : IProgressRepository
{
    public async Task<IReadOnlyList<ReportingCycle>> ListCyclesAsync(Guid projectId, CancellationToken cancellationToken) =>
        await Cycles(projectId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<(IReadOnlyList<ReportingCycle> Items, int TotalCount)> PageCyclesAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(Cycles(projectId), page, cancellationToken);

    public Task<ReportingCycle?> FindCycleAsync(Guid reportingCycleId, CancellationToken cancellationToken) =>
        context.Set<ReportingCycle>().SingleOrDefaultAsync(c => c.Id == reportingCycleId, cancellationToken);

    public async Task<ProgressSubmission?> FindSubmissionAsync(Guid submissionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProgressSubmission? submission = await context.Set<ProgressSubmission>().SingleOrDefaultAsync(s => s.Id == submissionId, cancellationToken).ConfigureAwait(false);
        if (submission is not null)
        {
            AdministrationPersistence.ExpectVersion(context, submission, expectedVersion);
        }

        return submission;
    }

    public uint RowVersionOf(ProgressSubmission submission) =>
        context.Entry(submission).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public Task<(IReadOnlyList<ProgressSubmission> Items, int TotalCount)> PageSubmissionsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(
            context.Set<ProgressSubmission>().AsNoTracking().Where(s => s.ProjectId == projectId).OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id),
            page, cancellationToken);

    public async Task<IReadOnlyList<ProgressSubmission>> ListRevisionsAsync(Guid reportingCycleId, CancellationToken cancellationToken) =>
        await context.Set<ProgressSubmission>().AsNoTracking().Where(s => s.ReportingCycleId == reportingCycleId).OrderBy(s => s.RevisionNo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<ProgressSubmission?> FindLatestPublishedSubmissionAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Set<ProgressSubmission>().AsNoTracking()
            .Where(s => s.ProjectId == projectId && s.Status == ProgressSubmissionStatus.Published)
            .OrderByDescending(s => s.ReviewedAt).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOpeningPositionAsync(Guid projectIntakeId, CancellationToken cancellationToken) =>
        context.Set<ProgressSubmission>().AnyAsync(s => s.ProjectIntakeId == projectIntakeId, cancellationToken);

    public Task<(IReadOnlyList<PublishedProgressSnapshot> Items, int TotalCount)> PageSnapshotsAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        PageAsync(Snapshots(projectId), page, cancellationToken);

    public Task<PublishedProgressSnapshot?> FindLatestSnapshotAsync(Guid projectId, CancellationToken cancellationToken) =>
        Snapshots(projectId).FirstOrDefaultAsync(cancellationToken);

    public Task<ProjectHealthStatus?> FindHealthStatusAsync(Guid projectId, bool track, CancellationToken cancellationToken)
    {
        IQueryable<ProjectHealthStatus> rows = context.Set<ProjectHealthStatus>();
        return (track ? rows : rows.AsNoTracking()).SingleOrDefaultAsync(h => h.ProjectId == projectId, cancellationToken);
    }

    public void Add(ReportingCycle cycle) => context.Add(cycle);

    public void Add(ProgressSubmission submission) => context.Add(submission);

    public void Add(PublishedProgressSnapshot snapshot) => context.Add(snapshot);

    public void Add(ProjectHealthStatus healthStatus) => context.Add(healthStatus);

    public async Task<ProgressSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ProgressSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return ProgressSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A period, a revision of one, a period's snapshot or a project's live health row that another request wrote first.
            context.ChangeTracker.Clear();
            return ProgressSaveOutcome.Duplicate;
        }
    }

    private IQueryable<ReportingCycle> Cycles(Guid projectId) =>
        context.Set<ReportingCycle>().AsNoTracking().Where(c => c.ProjectId == projectId).OrderBy(c => c.PeriodStart);

    /// <summary>Publication follows the periods' order (progress-update.md D-5), so the latest published is the latest period.</summary>
    private IQueryable<PublishedProgressSnapshot> Snapshots(Guid projectId) =>
        context.Set<PublishedProgressSnapshot>().AsNoTracking().Where(s => s.ProjectId == projectId).OrderByDescending(s => s.PublishedAt).ThenByDescending(s => s.Id);

    private static async Task<(IReadOnlyList<T> Items, int TotalCount)> PageAsync<T>(IQueryable<T> rows, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<T> items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }
}
