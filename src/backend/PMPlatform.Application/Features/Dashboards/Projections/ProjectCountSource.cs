using PMPlatform.Application.Features.Dashboards.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// A projection whose value is a set of counts its owning module makes for one project (CURRENT/LIVE, read on demand): each record counted
/// belongs to one project, so the counts add across a population (FG-01 §8). The owner's reader is the one WF-10's readiness reads (ADR-003
/// §8.2 edges 39 to 45) and is called once per project; it authorizes no one.
/// </summary>
internal abstract class ProjectCountSource : IDashboardProjectionSource
{
    public abstract string ProjectionCode { get; }

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Observation> observations = await ObserveAsync(request, cancellationToken).ConfigureAwait(false);
        return request.Context == DashboardContextKind.Project
            ? ProjectionReadings.Of(observations.Single())
            : ProjectionReadings.Aggregate(observations, counted => ProjectionReadings.SumBuckets(counted, []));
    }

    public async Task<IReadOnlyList<Observation>> ObserveAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<Observation> observations = [];
        foreach (Guid projectId in request.ProjectIds)
        {
            IReadOnlyList<WidgetFigure> counts = await CountAsync(projectId, cancellationToken).ConfigureAwait(false);
            observations.Add(new Observation(projectId, ObservationKind.Current, request.Now, new WidgetData(null, counts, ProjectionReadings.NoBuckets, ProjectionReadings.NoSeries)));
        }

        return observations;
    }

    /// <summary>The project's counts, as COUNT figures named by the projection's fields.</summary>
    protected abstract Task<IReadOnlyList<WidgetFigure>> CountAsync(Guid projectId, CancellationToken cancellationToken);
}
