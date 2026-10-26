using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>PROJECT.LIFECYCLE_STATE: WF-01's lifecycle state, each project once (BR-DSH-020), read live.</summary>
internal sealed class ProjectLifecycleSource : IDashboardProjectionSource
{
    private static readonly IReadOnlyList<string> Order = ProjectionReadings.Names<ProjectLifecycleState>();

    public string ProjectionCode => DashboardProjections.ProjectLifecycleState;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken) =>
        request.Read(await ObserveAsync(request, cancellationToken).ConfigureAwait(false), counted => ProjectionReadings.Distribution(counted, Order));

    public Task<IReadOnlyList<Observation>> ObserveAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult<IReadOnlyList<Observation>>(
            [.. request.Projects.Select(p => new Observation(p.Id, ObservationKind.Current, request.Now, ProjectionReadings.State(ProjectionReadings.Name(p.Status))))]);
    }
}
