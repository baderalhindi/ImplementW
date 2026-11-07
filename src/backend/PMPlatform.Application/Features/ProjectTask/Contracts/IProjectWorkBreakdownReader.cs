namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 55 (Progress → ProjectTask, query): the live tasks of a project, for WF-02's roll-up (ADR-009). It authorizes no
/// one: the consumer decides what its caller may see. A cancelled task is not work, so it is left out.
/// </summary>
public interface IProjectWorkBreakdownReader
{
    public Task<IReadOnlyList<WorkBreakdownItem>> ListAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <summary>A task's place in the breakdown, its planned days and its entered percentage; null is not yet entered.</summary>
public sealed record WorkBreakdownItem(Guid Id, Guid? ParentId, int PlannedDurationDays, decimal? ActualPercentComplete);
