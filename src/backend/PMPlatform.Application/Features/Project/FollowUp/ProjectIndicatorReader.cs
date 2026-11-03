using PMPlatform.Application.Features.Project.Contracts;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.FollowUp;

/// <summary>
/// RPMO's calculated columns for a set of projects (R3-3): the thresholds in force and the update-request counts read once, then each
/// project's indicators from them.
/// </summary>
internal sealed class ProjectIndicatorReader(OnPlanPolicy policy, IProjectUpdateComplianceSource updates, TimeProvider timeProvider)
{
    public async Task<Func<ProjectEntity, ProjectIndicators>> ForAsync(IReadOnlyCollection<ProjectEntity> projects, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projects);
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        OnPlanThresholds thresholds = await policy.ThresholdsAsync(now, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, UpdateComplianceCounts> counts = projects.Count == 0
            ? new Dictionary<Guid, UpdateComplianceCounts>()
            : await updates.CountAsync([.. projects.Select(p => p.Id)], today, cancellationToken).ConfigureAwait(false);
        return project => ProjectIndicatorCalculator.Of(project, thresholds, today, counts.GetValueOrDefault(project.Id));
    }

    public async Task<ProjectIndicators> OfAsync(ProjectEntity project, CancellationToken cancellationToken) =>
        (await ForAsync([project], cancellationToken).ConfigureAwait(false))(project);
}
