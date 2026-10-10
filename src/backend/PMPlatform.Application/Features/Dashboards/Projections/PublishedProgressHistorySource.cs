using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Progress.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// PROGRESS.PUBLISHED_PROGRESS_HISTORY: the project's immutable Published Progress Snapshots, oldest first (HISTORICAL/SNAPSHOT) — WF-02's
/// own history, never rebuilt from current records (BR-DSH-018, DSH-CC-25). A widget is bounded to the latest <see cref="Points"/> periods (FG-01 §29);
/// a report row set to <see cref="ReportPoints"/>.
/// </summary>
internal sealed class PublishedProgressHistorySource(IProjectHealthReader health, IReportingCycleReader cycles) : IDashboardProjectionSource
{
    public const int Points = 24;

    /// <summary>The bound of a report's history: ten years of monthly periods.</summary>
    public const int ReportPoints = 120;

    public string ProjectionCode => DashboardProjections.PublishedProgressHistory;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Observation observation = await ObserveAsync(request.ProjectIds.Single(), Points, request.Today, cancellationToken).ConfigureAwait(false);
        return observation.Kind == ObservationKind.Missing ? ProjectionReading.Unknown(WidgetUnknownReason.Missing) : ProjectionReadings.Of(observation);
    }

    /// <summary>Each project's whole published history, oldest first (FG-02 RPT-PRG-002), up to <see cref="ReportPoints"/> snapshots.</summary>
    public async Task<IReadOnlyList<Observation>> ObserveAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<Observation> observations = [];
        foreach (Guid projectId in request.ProjectIds)
        {
            observations.Add(await ObserveAsync(projectId, ReportPoints, request.Today, cancellationToken).ConfigureAwait(false));
        }

        return observations;
    }

    private async Task<Observation> ObserveAsync(Guid projectId, int count, DateOnly today, CancellationToken cancellationToken)
    {
        IReadOnlyList<PublishedProgressSnapshotDetail> published = await health.ListPublishedAsync(projectId, count, cancellationToken).ConfigureAwait(false);
        if (published.Count == 0)
        {
            return new Observation(projectId, ObservationKind.Missing, null, null);
        }

        IReadOnlyList<ReportingCycleSummary> periods = await cycles.ListAsync(projectId, cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, ReportingCycleSummary> byId = periods.ToDictionary(c => c.Id);
        List<WidgetSeriesPoint> series = [.. published.Reverse().Select(s => new WidgetSeriesPoint(
            s.PublishedAt,
            byId.GetValueOrDefault(s.ReportingCycleId)?.PeriodStart,
            byId.GetValueOrDefault(s.ReportingCycleId)?.PeriodEnd,
            ProjectionReadings.Name(s.OverallHealth),
            [ProjectionReadings.Percent("ACTUAL_PERCENT", s.ActualPercent), ProjectionReadings.Percent("PLANNED_PERCENT", s.PlannedPercent)]))];
        ObservationKind kind = ReportingStaleness.OverduePeriods(periods, today) > 0 ? ObservationKind.Stale : ObservationKind.Current;
        return new Observation(projectId, kind, published[0].PublishedAt, new WidgetData(null, ProjectionReadings.NoFigures, ProjectionReadings.NoBuckets, series));
    }
}
