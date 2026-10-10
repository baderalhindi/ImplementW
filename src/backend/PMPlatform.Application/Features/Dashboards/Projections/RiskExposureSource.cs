using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Risk.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// RISK.RISK_EXPOSURE: open risks by the rating their latest assessment recorded (WF-06, CURRENT/LIVE), read through
/// <see cref="IRiskExposureReader"/>. A risk not yet assessed is NOT_ASSESSED, never given a rating (BR-DSH-004); counts add across
/// projects because each risk belongs to one project.
/// </summary>
internal sealed class RiskExposureSource(IRiskExposureReader risks) : IDashboardProjectionSource
{
    public const string NotAssessed = "NOT_ASSESSED";

    public string ProjectionCode => DashboardProjections.RiskExposure;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<RiskExposure> exposures = await risks.ListAsync(request.ProjectIds, request.Today, cancellationToken).ConfigureAwait(false);

        // The ratings in their matrices' order, then the unassessed: the source's own order of its values.
        IReadOnlyList<string> order =
        [
            .. exposures.SelectMany(e => e.ByRating).GroupBy(r => r.RatingCode, StringComparer.Ordinal)
                .OrderBy(g => g.Min(r => r.SortOrder)).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key),
            NotAssessed,
        ];
        List<Observation> observations = [.. exposures.Select(e => new Observation(e.ProjectId, ObservationKind.Current, request.Now, new WidgetData(
            null,
            [ProjectionReadings.Count("OPEN_RISKS", e.OpenCount), ProjectionReadings.Count("NOT_ASSESSED", e.NotAssessedCount), ProjectionReadings.Count("REVIEW_OVERDUE", e.ReviewOverdueCount)],
            [.. e.ByRating.Select(r => new WidgetBucket(r.RatingCode, r.Label, r.Count)), .. e.NotAssessedCount > 0 ? [new WidgetBucket(NotAssessed, null, e.NotAssessedCount)] : Array.Empty<WidgetBucket>()],
            ProjectionReadings.NoSeries)))];
        return request.Context == DashboardContextKind.Project
            ? ProjectionReadings.Of(observations.Single() with { Data = ProjectionReadings.SumBuckets(observations, order) })
            : ProjectionReadings.Aggregate(observations, counted => ProjectionReadings.SumBuckets(counted, order));
    }
}
