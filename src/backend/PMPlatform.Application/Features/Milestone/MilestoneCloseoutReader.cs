using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Domain.Milestone;

namespace PMPlatform.Application.Features.Milestone;

/// <summary>A project's achievement claims still on their way, for WF-10's readiness (edge 41).</summary>
internal sealed class MilestoneCloseoutReader(IMilestoneRepository repository) : IMilestoneCloseoutReader
{
    public async Task<int> CountOpenClaimsAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await repository.CountByStatusAsync(projectId, cancellationToken).ConfigureAwait(false))
        .Where(c => c.Key is MilestoneAchievementStatus.Draft or MilestoneAchievementStatus.Submitted or MilestoneAchievementStatus.Returned)
        .Sum(c => c.Value);
}
