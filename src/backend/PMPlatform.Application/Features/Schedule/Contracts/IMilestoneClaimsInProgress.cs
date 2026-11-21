namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// WF-05's answer to WF-03 before a milestone is deleted (client review, item 8): whether an achievement claim of it awaits its decision.
/// Implemented by the Milestone module, which owns the claims.
/// </summary>
public interface IMilestoneClaimsInProgress
{
    public Task<bool> HasPendingAsync(Guid projectMilestoneId, CancellationToken cancellationToken);
}
