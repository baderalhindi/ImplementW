using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>A shared milestone as WF-03 holds it, for WF-05 (edge 10, ICD-04).</summary>
internal sealed class ProjectMilestoneReader(IScheduleRepository repository) : IProjectMilestoneReader
{
    public async Task<ProjectMilestoneFacts?> FindAsync(Guid projectMilestoneId, CancellationToken cancellationToken) =>
        await repository.ReadMilestoneAsync(projectMilestoneId, cancellationToken).ConfigureAwait(false) is { } m
            ? new ProjectMilestoneFacts(m.Id, m.ProjectId, m.MilestoneCategoryItemId, m.Status, m.ForecastDate)
            : null;
}
