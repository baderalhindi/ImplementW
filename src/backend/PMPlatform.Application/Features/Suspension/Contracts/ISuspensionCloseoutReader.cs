namespace PMPlatform.Application.Features.Suspension.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 45, Closure → Suspension (query; TASK-063): what WF-10's readiness needs of a project's suspension and resumption
/// requests. WF-10 never withdraws one. It authorizes no one.
/// </summary>
public interface ISuspensionCloseoutReader
{
    /// <summary>The project's requests not yet REJECTED, WITHDRAWN or EFFECTED.</summary>
    public Task<int> CountOpenAsync(Guid projectId, CancellationToken cancellationToken);
}
