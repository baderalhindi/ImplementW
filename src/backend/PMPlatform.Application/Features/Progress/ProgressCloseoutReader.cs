using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>A project's unpublished progress submissions, and whether it has published any, for WF-10's readiness (edge 46).</summary>
internal sealed class ProgressCloseoutReader(IProgressRepository repository) : IProgressCloseoutReader
{
    public async Task<ProgressCloseoutPosition> ReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<ProgressSubmissionStatus, int> counts = await repository.CountByStatusAsync(projectId, cancellationToken).ConfigureAwait(false);
        return new ProgressCloseoutPosition(
            counts.Where(c => c.Key != ProgressSubmissionStatus.Published).Sum(c => c.Value),
            counts.GetValueOrDefault(ProgressSubmissionStatus.Published) > 0);
    }
}
