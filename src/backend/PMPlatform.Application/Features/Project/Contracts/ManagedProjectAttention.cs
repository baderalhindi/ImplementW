namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// What waits on one project the caller manages (review round 8, point 13): the earliest progress and financial reporting period due by the end
/// of this month and not yet published, and the tasks and milestones past their dates. A project that never started an update has no period
/// yet, so none is due for it: the same rule as FG-01's reporting completeness.
/// </summary>
public sealed record ManagedProjectAttention(Guid ProjectId, DateOnly? UpdateDueDate, int OverdueTasks, int OverdueMilestones);

/// <summary>Every project the caller manages and may view, with what waits on it; <c>MonthEnd</c> is the last day "this month" covers.</summary>
public sealed record ManagedProjectAttentionList(DateOnly Today, DateOnly MonthEnd, IReadOnlyList<ManagedProjectAttention> Items);

public interface IManagedProjectAttentionService
{
    public Task<ManagedProjectAttentionList> ListAsync(Guid callerId, CancellationToken cancellationToken);
}
