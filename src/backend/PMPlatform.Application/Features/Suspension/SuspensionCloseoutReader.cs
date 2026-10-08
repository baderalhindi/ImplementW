using PMPlatform.Application.Features.Suspension.Contracts;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>A project's suspension and resumption requests not yet final, for WF-10's readiness (edge 45).</summary>
internal sealed class SuspensionCloseoutReader(ISuspensionRepository repository) : ISuspensionCloseoutReader
{
    public async Task<int> CountOpenAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await repository.CountByStatusAsync(projectId, cancellationToken).ConfigureAwait(false))
        .Where(c => !SuspensionWorkflow.IsFinal(c.Key))
        .Sum(c => c.Value);
}
