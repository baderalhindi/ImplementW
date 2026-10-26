using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval;

/// <summary>Where a run stands: its current stage is the lowest one with a PENDING task.</summary>
internal static class ApprovalStages
{
    public static short? Current(IEnumerable<ApprovalTask> tasks) =>
        tasks.Where(t => t.Status == ApprovalTaskStatus.Pending).Min(t => (short?)t.SequenceNo);

    /// <summary>A task can be decided now: it and its run are PENDING, and its stage is the current one.</summary>
    public static bool IsActionable(ApprovalInstance instance, IReadOnlyList<ApprovalTask> tasks, ApprovalTask task) =>
        instance.Status == ApprovalInstanceStatus.Pending
        && task.Status == ApprovalTaskStatus.Pending
        && Current(tasks) == task.SequenceNo;

    /// <summary>A task created by an escalation is not escalated again: the escalation role is the last resort.</summary>
    public static bool IsEscalationTarget(IReadOnlyList<ApprovalTask> tasks, ApprovalTask task) =>
        tasks.Any(t => t.EscalatedToTaskId == task.Id);
}
