namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 43, Closure → ManagementConcern (query; TASK-063): what WF-10's readiness needs of a project's issues and
/// challenges. WF-10 never resolves or closes one (BR-CLO-014). It authorizes no one.
/// </summary>
public interface IConcernCloseoutReader
{
    /// <summary>The project's concerns neither RESOLVED nor CLOSED.</summary>
    public Task<int> CountOpenAsync(Guid projectId, CancellationToken cancellationToken);
}
