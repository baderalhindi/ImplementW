using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// PROGRESS.PUBLISHED_SCHEDULE_HEALTH: WF-03's Schedule Health as the latest Published Progress Snapshot copied it (PUBLISHED/OFFICIAL) —
/// the schedule status an entity sees on its own project under PROGRESS_VIEW (ADR-013) without the schedule itself. A snapshot that had
/// none to copy is MISSING, never a colour. Stale as <see cref="PublishedProgressSource"/>.
/// </summary>
internal sealed class PublishedScheduleHealthSource(IProjectHealthReader health, IReportingCycleReader cycles) : IDashboardProjectionSource
{
    private static readonly IReadOnlyList<string> Order = ProjectionReadings.Names<HealthStatus>();

    public string ProjectionCode => DashboardProjections.PublishedScheduleHealth;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyDictionary<Guid, ProjectHealthView> views = await health.ListAsync(request.ProjectIds, cancellationToken).ConfigureAwait(false);
        ILookup<Guid, ReportingCycleSummary> periods = ReportingStaleness.ByProject(await cycles.ListAsync(request.ProjectIds, cancellationToken).ConfigureAwait(false));
        List<Observation> observations = [.. request.ProjectIds.Select(id => views[id].Published is { ScheduleHealth: { } scheduleHealth } published
            ? new Observation(
                id,
                ReportingStaleness.OverduePeriods(periods[id], request.Today) > 0 ? ObservationKind.Stale : ObservationKind.Current,
                published.PublishedAt,
                ProjectionReadings.State(ProjectionReadings.Name(scheduleHealth)))
            : new Observation(id, ObservationKind.Missing, null, null))];
        return request.Read(observations, counted => ProjectionReadings.Distribution(counted, Order));
    }
}
