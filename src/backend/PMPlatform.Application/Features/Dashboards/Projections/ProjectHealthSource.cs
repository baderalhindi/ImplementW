using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// PROGRESS.PROJECT_HEALTH_STATUS: Overall Project Health as WF-02 last computed it (CURRENT/LIVE), read through
/// <see cref="IProjectHealthReader"/> and never recomputed (ICD-03, BR-DSH-003). UNKNOWN is the source's own value and stays UNKNOWN.
/// </summary>
internal sealed class ProjectHealthSource(IProjectHealthReader health) : IDashboardProjectionSource
{
    private static readonly IReadOnlyList<string> Order = ProjectionReadings.Names<HealthStatus>();

    public string ProjectionCode => DashboardProjections.ProjectHealthStatus;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyDictionary<Guid, ProjectHealthView> views = await health.ListAsync(request.ProjectIds, cancellationToken).ConfigureAwait(false);
        List<Observation> observations = [.. request.ProjectIds.Select(id => views[id].Current is { } current
            ? new Observation(id, ObservationKind.Current, current.ComputedAt, ProjectionReadings.State(
                ProjectionReadings.Name(current.OverallHealth),
                ProjectionReadings.Percent("ACTUAL_PERCENT", current.ActualPercent),
                ProjectionReadings.Percent("PLANNED_PERCENT", current.PlannedPercent)))
            : new Observation(id, ObservationKind.Missing, null, null))];
        return request.Read(observations, counted => ProjectionReadings.Distribution(counted, Order));
    }
}
