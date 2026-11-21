using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Milestone;

namespace PMPlatform.Application.Features.Milestone;

/// <inheritdoc cref="IMilestoneClaimsInProgress"/>
internal sealed class MilestoneClaimsInProgress(IMilestoneRepository repository) : IMilestoneClaimsInProgress
{
    public async Task<bool> HasPendingAsync(Guid projectMilestoneId, CancellationToken cancellationToken) =>
        (await repository.ListRevisionsAsync(projectMilestoneId, cancellationToken).ConfigureAwait(false))
        .Any(a => a.Status == MilestoneAchievementStatus.Submitted);
}
