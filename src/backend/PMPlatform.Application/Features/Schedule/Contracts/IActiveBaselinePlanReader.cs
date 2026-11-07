namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 56 (Progress → Schedule, query): the planned dates of the project's active baseline, for WF-02's planned progress
/// (ADR-009). It authorizes no one. Null when no baseline is active or the active one has no plan.
/// </summary>
public interface IActiveBaselinePlanReader
{
    public Task<ActiveBaselinePlan?> FindAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <summary>The active baseline's leaf activities as planned.</summary>
public sealed record ActiveBaselinePlan(Guid BaselineId, IReadOnlyList<ActiveBaselineActivity> Activities);

public sealed record ActiveBaselineActivity(Guid Id, DateOnly PlannedStart, DateOnly PlannedFinish);
