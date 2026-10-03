using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>Schedule Health as WF-03 stored it, never recalculated (M-12).</summary>
internal sealed class ScheduleHealthReader(IScheduleRepository repository) : IScheduleHealthReader
{
    public async Task<ScheduleHealthStatusDetail?> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        await repository.FindHealthStatusAsync(projectId, track: false, cancellationToken).ConfigureAwait(false) is { } health ? ScheduleMapping.ToDetail(health) : null;
}
