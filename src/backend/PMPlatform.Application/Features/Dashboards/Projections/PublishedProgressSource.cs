using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT: the latest Published Progress Snapshot (PUBLISHED/OFFICIAL) — the official Overall Project Health
/// and the published percentages, with the override flag ADR-009 requires — never replaced by the live value (BR-DSH-006). STALE, value
/// kept and dated by its publication, while a later period is past due unpublished (<see cref="ReportingStaleness"/>).
/// </summary>
internal sealed class PublishedProgressSource(IProjectHealthReader health, IReportingCycleReader cycles) : IDashboardProjectionSource
{
    private static readonly IReadOnlyList<string> Order = ProjectionReadings.Names<HealthStatus>();

    public string ProjectionCode => DashboardProjections.PublishedProgressSnapshot;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyDictionary<Guid, ProjectHealthView> views = await health.ListAsync(request.ProjectIds, cancellationToken).ConfigureAwait(false);
        ILookup<Guid, ReportingCycleSummary> periods = ReportingStaleness.ByProject(await cycles.ListAsync(request.ProjectIds, cancellationToken).ConfigureAwait(false));
        List<Observation> observations = [.. request.ProjectIds.Select(id => views[id].Published is { } published
            ? new Observation(
                id,
                ReportingStaleness.OverduePeriods(periods[id], request.Today) > 0 ? ObservationKind.Stale : ObservationKind.Current,
                published.PublishedAt,
                ProjectionReadings.State(
                    ProjectionReadings.Name(published.OverallHealth),
                    ProjectionReadings.Percent("ACTUAL_PERCENT", published.ActualPercent),
                    ProjectionReadings.Percent("PLANNED_PERCENT", published.PlannedPercent),
                    new WidgetFigure("ACTUAL_PERCENT_OVERRIDDEN", published.IsOverridden ? "true" : "false", "BOOLEAN", false)))
            : new Observation(id, ObservationKind.Missing, null, null))];
        return request.Read(observations, counted => ProjectionReadings.Distribution(counted, Order));
    }
}
