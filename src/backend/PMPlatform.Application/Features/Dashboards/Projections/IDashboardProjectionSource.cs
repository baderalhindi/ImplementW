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

    /// <summary>The widget's reading: one project's value, or the population's aggregate.</summary>
    public Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Each project's value as the source states it, one observation per project of the request and in its order: what a report row presents
    /// (FG-02, edge 30). The same values <see cref="ReadAsync"/> counts.
    /// </summary>
    public Task<IReadOnlyList<Observation>> ObserveAsync(ProjectionRequest request, CancellationToken cancellationToken);
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

/// <summary>The adapters by projection code, as a widget and a report row find them.</summary>
internal static class ProjectionSources
{
    /// <summary>One adapter per projection; a later registration replaces an earlier one, as a service registration does.</summary>
    public static Dictionary<string, IDashboardProjectionSource> ByCode(IEnumerable<IDashboardProjectionSource> sources) =>
        sources.GroupBy(s => s.ProjectionCode, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
}
