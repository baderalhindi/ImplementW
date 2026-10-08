using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Domain.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>A project's issues and challenges not yet resolved, for WF-10's readiness (edge 43).</summary>
internal sealed class ConcernCloseoutReader(IManagementConcernRepository repository) : IConcernCloseoutReader
{
    public async Task<int> CountOpenAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await repository.CountByStatusAsync(projectId, cancellationToken).ConfigureAwait(false))
        .Where(c => c.Key is not (ConcernStatus.Resolved or ConcernStatus.Closed))
        .Sum(c => c.Value);
}
