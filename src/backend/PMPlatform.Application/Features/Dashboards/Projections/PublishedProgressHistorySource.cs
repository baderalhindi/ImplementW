using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Progress.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// PROGRESS.PUBLISHED_PROGRESS_HISTORY: the project's immutable Published Progress Snapshots, oldest first (HISTORICAL/SNAPSHOT) — WF-02's
/// own history, never rebuilt from current records (BR-DSH-018, DSH-CC-25). Bounded to the latest <see cref="Points"/> periods (FG-01 §29).
/// </summary>
internal sealed class PublishedProgressHistorySource(IProjectHealthReader health, IReportingCycleReader cycles) : IDashboardProjectionSource
{
    public const int Points = 24;

    public string ProjectionCode => DashboardProjections.PublishedProgressHistory;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid projectId = request.ProjectIds.Single();
        IReadOnlyList<PublishedProgressSnapshotDetail> published = await health.ListPublishedAsync(projectId, Points, cancellationToken).ConfigureAwait(false);
        if (published.Count == 0)
        {
            return ProjectionReading.Unknown(WidgetUnknownReason.Missing);
        }

        IReadOnlyList<ReportingCycleSummary> periods = await cycles.ListAsync(projectId, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, ReportingCycleSummary> byId = periods.ToDictionary(c => c.Id);
        List<WidgetSeriesPoint> series = [.. published.Reverse().Select(s => new WidgetSeriesPoint(
            s.PublishedAt,
            byId.GetValueOrDefault(s.ReportingCycleId)?.PeriodStart,
            byId.GetValueOrDefault(s.ReportingCycleId)?.PeriodEnd,
            ProjectionReadings.Name(s.OverallHealth),
            [ProjectionReadings.Percent("ACTUAL_PERCENT", s.ActualPercent), ProjectionReadings.Percent("PLANNED_PERCENT", s.PlannedPercent)]))];
        ObservationKind kind = ReportingStaleness.OverduePeriods(periods, request.Today) > 0 ? ObservationKind.Stale : ObservationKind.Current;
        return ProjectionReadings.Of(new Observation(projectId, kind, published[0].PublishedAt,
            new WidgetData(null, ProjectionReadings.NoFigures, ProjectionReadings.NoBuckets, series)));
    }
}
