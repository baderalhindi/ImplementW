using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation.MonthlyUpdates;

/// <summary>Review round 7, item 2: the project's open update requests the outgoing Project Manager answers pass to the new one.</summary>
internal sealed class UpdateRequestManagerHandover(IExternalParticipationRepository repository) : IProjectManagerHandover
{
    public string Records => "update_requests";

    public async Task<int> HandOverAsync(Guid projectId, Guid fromUserId, Guid toUserId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExternalUpdateRequest> open = await repository.ListOpenRequestsOfAsync(projectId, fromUserId, cancellationToken).ConfigureAwait(false);
        foreach (ExternalUpdateRequest request in open)
        {
            request.ResponsibleUserId = toUserId;
            request.UpdatedAt = now;
            request.UpdatedBy = actorId;
        }

        return open.Count;
    }
}
