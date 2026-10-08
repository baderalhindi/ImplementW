namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 39, Closure → ProjectTask (query; TASK-063): what WF-10's readiness needs of a project's tasks. WF-04 decides what a
/// task still to be dispositioned is (WF-10 P4); WF-10 never completes or cancels one (BR-CLO-011). It authorizes no one.
/// </summary>
public interface IProjectTaskCloseoutReader
{
    /// <summary>The project's tasks and subtasks neither COMPLETED nor CANCELLED.</summary>
    public Task<int> CountOpenAsync(Guid projectId, CancellationToken cancellationToken);
}
