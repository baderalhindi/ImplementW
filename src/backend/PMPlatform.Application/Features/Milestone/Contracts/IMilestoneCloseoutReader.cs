namespace PMPlatform.Application.Features.Milestone.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 41, Closure → Milestone (query; TASK-063): what WF-10's readiness needs of a project's achievement claims. WF-10
/// never marks a milestone achieved (BR-CLO-012). It authorizes no one.
/// </summary>
public interface IMilestoneCloseoutReader
{
    /// <summary>The project's achievement claims DRAFT, SUBMITTED or RETURNED: neither accepted nor superseded.</summary>
    public Task<int> CountOpenClaimsAsync(Guid projectId, CancellationToken cancellationToken);
}
