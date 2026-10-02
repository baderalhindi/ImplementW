using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// Where WF-02 reads the facts it derives progress and health from, each owned by another module: task-level actual
/// progress (WF-04), the active baseline (WF-03), Schedule Health (WF-03) and the financial status (WF-14). ADR-003 §8.2
/// has no edge from Progress to any of them yet, and none of them is built, so the only implementation is
/// <see cref="NoProgressInputs"/>; each source is connected when its module and its §8.2 edge exist
/// (progress-update.md F-1).
/// </summary>
internal interface IProgressInputs
{
    public Task<ProgressInputs> ReadAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <summary>The inputs as read now. An empty work breakdown, or a null, is a source with nothing to give.</summary>
internal sealed record ProgressInputs(
    IReadOnlyList<WorkItemProgress> WorkBreakdown, BaselinePlan? Baseline, HealthStatus? ScheduleHealth, HealthStatus? FinancialStatus)
{
    public static ProgressInputs None { get; } = new([], null, null, null);
}

/// <summary>
/// One item of the work breakdown (WF-04): its planned duration and, on a leaf, the actual percentage its owner maintains.
/// A summary item's own percentage is derived, so it is not read.
/// </summary>
internal sealed record WorkItemProgress(Guid Id, Guid? ParentId, decimal PlannedDurationDays, decimal ActualPercent);

/// <summary>The active baseline (WF-03), as its leaf activities' planned dates. A Declared Baseline with no plan has none.</summary>
internal sealed record BaselinePlan(Guid BaselineId, IReadOnlyList<BaselineActivity> Activities);

internal sealed record BaselineActivity(Guid Id, DateOnly PlannedStart, DateOnly PlannedFinish);

/// <summary>Until WF-03, WF-04 and WF-14 are connected (progress-update.md F-1): nothing to derive from.</summary>
internal sealed class NoProgressInputs : IProgressInputs
{
    public Task<ProgressInputs> ReadAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult(ProgressInputs.None);
}
