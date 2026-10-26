namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 9: the activity identity and dates a WF-04 task executes against. It authorizes no one: the consumer
/// decides what its caller may see, on the project's anchors.
/// </summary>
public interface IScheduleActivityReader
{
    /// <summary>Null when there is no such activity.</summary>
    public Task<ScheduleActivityFacts?> FindAsync(Guid activityId, CancellationToken cancellationToken);
}
