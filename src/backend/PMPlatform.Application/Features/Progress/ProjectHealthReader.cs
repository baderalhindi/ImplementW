using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>ICD-03 for the read side: what WF-02 published and what it computed last, read as stored, never recalculated.</summary>
internal sealed class ProjectHealthReader(IProgressRepository repository) : IProjectHealthReader
{
    public async Task<ProjectHealthView> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        new(await repository.FindLatestSnapshotAsync(projectId, cancellationToken).ConfigureAwait(false) is { } published ? ProgressMapping.ToDetail(published) : null,
            await repository.FindHealthStatusAsync(projectId, track: false, cancellationToken).ConfigureAwait(false) is { } current ? ProgressMapping.ToDetail(current) : null);

    public async Task<IReadOnlyDictionary<Guid, ProjectHealthView>> ListAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        Dictionary<Guid, PublishedProgressSnapshot> published = (await repository.ListLatestSnapshotsAsync(projectIds, cancellationToken).ConfigureAwait(false)).ToDictionary(s => s.ProjectId);
        Dictionary<Guid, ProjectHealthStatus> current = (await repository.ListHealthStatusesAsync(projectIds, cancellationToken).ConfigureAwait(false)).ToDictionary(h => h.ProjectId);
        return projectIds.Distinct().ToDictionary(
            id => id,
            id => new ProjectHealthView(
                published.TryGetValue(id, out PublishedProgressSnapshot? s) ? ProgressMapping.ToDetail(s) : null,
                current.TryGetValue(id, out ProjectHealthStatus? h) ? ProgressMapping.ToDetail(h) : null));
    }

    public async Task<IReadOnlyList<PublishedProgressSnapshotDetail>> ListPublishedAsync(Guid projectId, int count, CancellationToken cancellationToken) =>
        [.. (await repository.PageSnapshotsAsync(projectId, new PageRequest(1, count), cancellationToken).ConfigureAwait(false)).Items.Select(ProgressMapping.ToDetail)];
}
