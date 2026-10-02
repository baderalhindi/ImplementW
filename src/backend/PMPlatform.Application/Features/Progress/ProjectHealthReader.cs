using PMPlatform.Application.Features.Progress.Contracts;

namespace PMPlatform.Application.Features.Progress;

/// <summary>ICD-03 for the read side: what WF-02 published and what it computed last, read as stored, never recalculated.</summary>
internal sealed class ProjectHealthReader(IProgressRepository repository) : IProjectHealthReader
{
    public async Task<ProjectHealthView> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        new(await repository.FindLatestSnapshotAsync(projectId, cancellationToken).ConfigureAwait(false) is { } published ? ProgressMapping.ToDetail(published) : null,
            await repository.FindHealthStatusAsync(projectId, track: false, cancellationToken).ConfigureAwait(false) is { } current ? ProgressMapping.ToDetail(current) : null);
}
