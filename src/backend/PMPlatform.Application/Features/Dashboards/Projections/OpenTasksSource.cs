using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>PROJECT_TASK.OPEN_TASKS: WF-04's tasks and subtasks neither COMPLETED nor CANCELLED, through <see cref="IProjectTaskCloseoutReader"/> (edge 48).</summary>
internal sealed class OpenTasksSource(IProjectTaskCloseoutReader tasks) : ProjectCountSource
{
    public override string ProjectionCode => DashboardProjections.OpenTasks;

    protected override async Task<IReadOnlyList<WidgetFigure>> CountAsync(Guid projectId, CancellationToken cancellationToken) =>
        [ProjectionReadings.Count("OPEN_TASKS", await tasks.CountOpenAsync(projectId, cancellationToken).ConfigureAwait(false))];
}
