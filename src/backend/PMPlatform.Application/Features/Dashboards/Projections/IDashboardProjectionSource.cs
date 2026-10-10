using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// The adapter of one registered projection (FG-01 §24 Source-Domain Projection Adapters): it reads the owning module through that
/// module's contracts and states each project's value as the source does. It decides no access: the composition hands it only the
/// projects the caller may see under the projection's permission and that the projection expects a value from.
/// </summary>
internal interface IDashboardProjectionSource
{
    public string ProjectionCode { get; }

    public Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// What a source reads for: the caller, the context, the projects (exactly one in the Project context, none for a projection not
/// about projects), and the moment the dashboard is assembled.
/// </summary>
internal sealed record ProjectionRequest(Guid CallerId, DashboardContextKind Context, IReadOnlyList<ProjectFacts> Projects, DateTimeOffset Now)
{
    public IReadOnlyList<Guid> ProjectIds { get; } = [.. Projects.Select(p => p.Id)];

    public DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);

    /// <summary>One project's reading in the Project context; the population's otherwise.</summary>
    public ProjectionReading Read(IReadOnlyList<Observation> observations, Func<IReadOnlyList<Observation>, WidgetData> combine) =>
        Context == DashboardContextKind.Project ? ProjectionReadings.Of(observations.Single()) : ProjectionReadings.Aggregate(observations, combine);
}
