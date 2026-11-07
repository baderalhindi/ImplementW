using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// Where WF-02 reads the facts it derives progress and health from, each owned by another module: task-level actual
/// progress (WF-04), the active baseline (WF-03), Schedule Health (WF-03), the financial status (WF-14) and, for a project
/// with no schedule to calculate from, the figures declared on it (WF-01). See <see cref="ProgressInputsReader"/>.
/// </summary>
internal interface IProgressInputs
{
    public Task<ProgressInputs> ReadAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <summary>The inputs as read now. An empty work breakdown, or a null, is a source with nothing to give.</summary>
internal sealed record ProgressInputs(
    IReadOnlyList<WorkItemProgress> WorkBreakdown,
    BaselinePlan? Baseline,
    HealthStatus? ScheduleHealth,
    HealthStatus? FinancialStatus,
    decimal? DeclaredActualPercent = null)
{
    public static ProgressInputs None { get; } = new([], null, null, null);

    /// <summary>The actual figure and where it comes from: the work breakdown when it gives one, else the declared figure, else nothing yet.</summary>
    public (decimal? Percent, ProgressActualSource Source) Actual() =>
        ProgressRollup.Actual(WorkBreakdown) is { } rolledUp ? (rolledUp, ProgressActualSource.WorkBreakdown)
        : DeclaredActualPercent is { } declared ? (declared, ProgressActualSource.Declared)
        : (null, ProgressActualSource.NotAvailable);
}

/// <summary>
/// One item of the work breakdown (WF-04): its planned duration and, on a leaf, the actual percentage its owner maintains.
/// A summary item's own percentage is derived, so it is not read.
/// </summary>
internal sealed record WorkItemProgress(Guid Id, Guid? ParentId, decimal PlannedDurationDays, decimal ActualPercent);

/// <summary>The active baseline (WF-03), as its leaf activities' planned dates. A Declared Baseline with no plan has none.</summary>
internal sealed record BaselinePlan(Guid BaselineId, IReadOnlyList<BaselineActivity> Activities);

internal sealed record BaselineActivity(Guid Id, DateOnly PlannedStart, DateOnly PlannedFinish);

