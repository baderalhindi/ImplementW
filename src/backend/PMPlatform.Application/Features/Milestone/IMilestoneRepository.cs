using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Milestone;

namespace PMPlatform.Application.Features.Milestone;

/// <summary>The <c>milestone</c> schema (TASK-050). Finds that return rows to change track them; the others do not.</summary>
public interface IMilestoneRepository
{
    /// <summary>A transaction for the writes that follow, or the caller's own when one is open already (an outcome dispatch).</summary>
    public Task<IMilestoneWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Every revision of the milestone. Not tracked.</summary>
    public Task<IReadOnlyList<MilestoneAchievement>> ListRevisionsAsync(Guid projectMilestoneId, CancellationToken cancellationToken);

    /// <summary>One page of the revisions of a milestone, or of a project's milestones, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<MilestoneAchievement> Items, int TotalCount)> PageAsync(
        Guid? projectMilestoneId, Guid? projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<MilestoneAchievement?> FindAsync(Guid achievementId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The milestone's ACCEPTED revision, tracked; null before its first acceptance.</summary>
    public Task<MilestoneAchievement?> FindAcceptedAsync(Guid projectMilestoneId, CancellationToken cancellationToken);

    /// <summary>How many of the project's achievements are in each status, for WF-10's readiness (TASK-063). Not tracked.</summary>
    public Task<IReadOnlyDictionary<MilestoneAchievementStatus, int>> CountByStatusAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked row, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(MilestoneAchievement achievement);

    public void Add(MilestoneAchievement achievement);

    /// <summary>HARD_DRAFT.</summary>
    public void Remove(MilestoneAchievement achievement);

    /// <summary>
    /// Saves the tracked changes and the audit events this unit of work staged. A row changed since it was read, and a unique
    /// key another request took first, are answers, not faults; after either nothing stays tracked and the unit of work is lost.
    /// </summary>
    public Task<MilestoneSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A unit of work over the milestone schema; disposing it without <see cref="CommitAsync"/> rolls it back.</summary>
public interface IMilestoneWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum MilestoneSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key another request took first: the milestone's next revision, its open revision, or its ACCEPTED one.</summary>
    Duplicate = 3,
}
