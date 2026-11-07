using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <inheritdoc cref="IProjectWorkBreakdownReader"/>
internal sealed class ProjectWorkBreakdownReader(IProjectTaskRepository repository) : IProjectWorkBreakdownReader
{
    public async Task<IReadOnlyList<WorkBreakdownItem>> ListAsync(Guid projectId, CancellationToken cancellationToken) =>
        [.. (await repository.ListTasksAsync(projectId, track: false, cancellationToken).ConfigureAwait(false))
            .Where(t => t.Status != ProjectTaskStatus.Cancelled)
            .Select(t => new WorkBreakdownItem(t.Id, t.ParentTaskId, t.PlannedDurationDays, t.ActualPercentComplete))];
}
