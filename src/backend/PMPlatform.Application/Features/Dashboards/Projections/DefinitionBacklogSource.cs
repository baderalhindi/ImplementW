using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// DASHBOARDS.DEFINITION_BACKLOG: FG-01's own configuration backlog for DSH-001 — dashboard versions on their way and in force, by
/// lifecycle state, read live. It is configuration, not business data, and needs only the configuration view (BR-DSH-026).
/// </summary>
internal sealed class DefinitionBacklogSource(IDashboardRepository repository) : IDashboardProjectionSource
{
    private static readonly GovernedLifecycleState[] Counted = [GovernedLifecycleState.Draft, GovernedLifecycleState.Validated, GovernedLifecycleState.Published];

    public string ProjectionCode => DashboardProjections.DefinitionBacklog;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyDictionary<GovernedLifecycleState, int> counts = await repository.CountByStateAsync(cancellationToken).ConfigureAwait(false);
        WidgetData data = new(
            null,
            ProjectionReadings.NoFigures,
            [.. Counted.Select(state => new WidgetBucket(ProjectionReadings.Name(state), null, counts.GetValueOrDefault(state)))],
            ProjectionReadings.NoSeries);
        return ProjectionReadings.Of(new Observation(Guid.Empty, ObservationKind.Current, request.Now, data));
    }

    /// <summary>Not about projects: it has no rows to give a report.</summary>
    public Task<IReadOnlyList<Observation>> ObserveAsync(ProjectionRequest request, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Observation>>([]);
}
