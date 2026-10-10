using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// SCHEDULE.SCHEDULE_HEALTH_STATUS: Schedule Health as WF-03 stored it (CURRENT/LIVE) with the finish variance in days, read through
/// <see cref="IScheduleHealthReader"/>; never re-derived from tasks (FG-01 §8, M-12).
/// </summary>
internal sealed class ScheduleHealthSource(IScheduleHealthReader schedules) : IDashboardProjectionSource
{
    private static readonly IReadOnlyList<string> Order = ProjectionReadings.Names<ScheduleHealth>();

    public string ProjectionCode => DashboardProjections.ScheduleHealthStatus;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<Guid, ScheduleHealthStatusDetail> stored = (await schedules.ListAsync(request.ProjectIds, cancellationToken).ConfigureAwait(false)).ToDictionary(h => h.ProjectId);
        List<Observation> observations = [.. request.ProjectIds.Select(id => stored.TryGetValue(id, out ScheduleHealthStatusDetail? h)
            ? new Observation(id, ObservationKind.Current, h.ComputedAt,
                ProjectionReadings.State(ProjectionReadings.Name(h.ScheduleHealth), ProjectionReadings.Days("FINISH_VARIANCE_DAYS", h.FinishVarianceDays)))
            : new Observation(id, ObservationKind.Missing, null, null))];
        return request.Read(observations, counted => ProjectionReadings.Distribution(counted, Order));
    }
}
