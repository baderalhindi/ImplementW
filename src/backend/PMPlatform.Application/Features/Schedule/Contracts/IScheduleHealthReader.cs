namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// Schedule Health for the read side and the modules that report it (ADR-003 §8.2 edge 29): the value WF-03 stored, never
/// a recalculation (M-12). It authorizes no one: the consumer decides what its viewer may see. Null until first computed.
/// </summary>
public interface IScheduleHealthReader
{
    public Task<ScheduleHealthStatusDetail?> GetAsync(Guid projectId, CancellationToken cancellationToken);
}
