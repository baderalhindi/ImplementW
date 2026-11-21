using PMPlatform.Application.Features.Project.Contracts;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>Review round 7, item 2: the project's open tasks the outgoing Project Manager owns pass to the new one.</summary>
internal sealed class ProjectTaskManagerHandover(IProjectTaskRepository repository) : IProjectManagerHandover
{
    public string Records => "tasks";

    public async Task<int> HandOverAsync(Guid projectId, Guid fromUserId, Guid toUserId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectTaskEntity> open = await repository.ListOpenTasksOfAsync(projectId, fromUserId, cancellationToken).ConfigureAwait(false);
        foreach (ProjectTaskEntity task in open)
        {
            task.AssigneeUserId = toUserId;
            task.UpdatedAt = now;
            task.UpdatedBy = actorId;
        }

        return open.Count;
    }
}
