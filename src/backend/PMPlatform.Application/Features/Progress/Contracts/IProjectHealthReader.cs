namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// ICD-03 for the read side (ADR-003 §8.2 edge 29): Overall Project Health as WF-02 published it, and the current value
/// beside it. A consumer renders these and never recalculates them (M-12). It authorizes no one: the consumer decides
/// what its viewer may see.
/// </summary>
public interface IProjectHealthReader
{
    public Task<ProjectHealthView> GetAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The view of each project named, keyed by project; a project with neither value yet has a view of two nulls (FG-01, TASK-069).</summary>
    public Task<IReadOnlyDictionary<Guid, ProjectHealthView>> ListAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>
    /// The project's published snapshots, latest first, at most <paramref name="count"/>: WF-02's own history, for a trend that is
    /// never reconstructed from current records (BR-DSH-018).
    /// </summary>
    public Task<IReadOnlyList<PublishedProgressSnapshotDetail>> ListPublishedAsync(Guid projectId, int count, CancellationToken cancellationToken);
}
