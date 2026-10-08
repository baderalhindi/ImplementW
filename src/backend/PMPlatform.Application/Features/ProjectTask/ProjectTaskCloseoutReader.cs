using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>A project's tasks still to be dispositioned, for WF-10's readiness (edge 39).</summary>
internal sealed class ProjectTaskCloseoutReader(IProjectTaskRepository repository) : IProjectTaskCloseoutReader
{
    public async Task<int> CountOpenAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await repository.ListTasksAsync(projectId, track: false, cancellationToken).ConfigureAwait(false))
        .Count(t => t.Status is not (ProjectTaskStatus.Completed or ProjectTaskStatus.Cancelled));
}
