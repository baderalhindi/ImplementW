namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 40, Closure → Schedule (query; TASK-063): what WF-10's readiness needs of a project's schedule. WF-10 never
/// rebaselines, cancels or achieves anything (BR-CLO-012, BR-CLO-017). It authorizes no one.
/// </summary>
public interface IScheduleCloseoutReader
{
    public Task<ScheduleCloseoutPosition> ReadAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <param name="OpenCandidates">Baseline candidates not yet ACTIVE, SUPERSEDED, REJECTED or WITHDRAWN.</param>
/// <param name="PlannedMilestones">Milestones neither ACHIEVED nor CANCELLED.</param>
public sealed record ScheduleCloseoutPosition(int OpenCandidates, int PlannedMilestones);
