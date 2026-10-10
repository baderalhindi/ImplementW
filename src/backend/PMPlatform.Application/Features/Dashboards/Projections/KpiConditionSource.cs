using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// FINANCIAL_KPI.KPI_CONDITION: ACTIVE KPI assignments counted by the RAG of their latest published measurement (PUBLISHED/OFFICIAL), as
/// WF-14 rated it against the pinned target. A condition is a count; no KPI value is combined with another (BR-DSH-022, DC-DSH-010). An
/// assignment with nothing published yet is NOT_PUBLISHED; a project with no ACTIVE assignment has no KPI condition to report.
/// </summary>
internal sealed class KpiConditionSource(IKpiConditionReader conditions) : IDashboardProjectionSource
{
    public const string NotPublished = "NOT_PUBLISHED";

    private static readonly IReadOnlyList<string> Order = [.. ProjectionReadings.Names<KpiRagStatus>(), NotPublished];

    public string ProjectionCode => DashboardProjections.KpiCondition;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ILookup<Guid, KpiCondition> byProject = (await conditions.ListAsync(request.ProjectIds, cancellationToken).ConfigureAwait(false)).ToLookup(c => c.ProjectId);
        List<Observation> observations = [.. request.ProjectIds.Select(id => Observe(id, [.. byProject[id]]))];
        return request.Context == DashboardContextKind.Project
            ? ProjectionReadings.Of(observations.Single() is { IsCounted: true } single ? single with { Data = ProjectionReadings.SumBuckets(observations, Order) } : observations.Single())
            : ProjectionReadings.Aggregate(observations, counted => ProjectionReadings.SumBuckets(counted, Order));
    }

    private static Observation Observe(Guid projectId, IReadOnlyList<KpiCondition> assignments)
    {
        if (assignments.Count == 0)
        {
            return new Observation(projectId, ObservationKind.NotApplicable, null, null);
        }

        List<KpiCondition> published = [.. assignments.Where(a => a.RagStatus is not null)];
        if (published.Count == 0)
        {
            return new Observation(projectId, ObservationKind.Missing, null, null);
        }

        ObservationKind kind = published.Any(a => a.ValueStatus == ValueStatus.Stale) ? ObservationKind.Stale : ObservationKind.Current;
        int notPublished = assignments.Count - published.Count;
        return new Observation(projectId, kind, FinancialReadings.AsOf(published.Min(a => a.AsOfDate)), new WidgetData(
            null,
            [ProjectionReadings.Count("ACTIVE_ASSIGNMENTS", assignments.Count), ProjectionReadings.Count(NotPublished, notPublished)],
            [.. published.GroupBy(a => a.RagStatus!.Value).Select(g => new WidgetBucket(ProjectionReadings.Name(g.Key), null, g.Count())),
                .. notPublished > 0 ? [new WidgetBucket(NotPublished, null, notPublished)] : Array.Empty<WidgetBucket>()],
            ProjectionReadings.NoSeries));
    }
}
