using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Domain.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>A project's change requests by how far each is from closed, for WF-10's readiness (edge 44).</summary>
internal sealed class ChangeRequestCloseoutReader(IChangeRequestRepository repository) : IChangeRequestCloseoutReader
{
    public async Task<ChangeRequestCloseoutPosition> ReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<ChangeRequestStatus, int> counts = await repository.CountByStatusAsync(projectId, cancellationToken).ConfigureAwait(false);
        int Of(params ChangeRequestStatus[] statuses) => statuses.Sum(s => counts.GetValueOrDefault(s));
        return new ChangeRequestCloseoutPosition(
            Of(ChangeRequestStatus.Draft, ChangeRequestStatus.Submitted, ChangeRequestStatus.UnderReview, ChangeRequestStatus.Returned),
            Of(ChangeRequestStatus.Approved),
            Of(ChangeRequestStatus.Implementation, ChangeRequestStatus.Implemented));
    }
}
