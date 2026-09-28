namespace PMPlatform.Application.Features.Approval;

/// <summary>The time-driven part of WF-11, run by a worker as the approval-workflow service principal.</summary>
public interface IApprovalMaintenance
{
    /// <summary>Escalates up to <paramref name="batchSize"/> overdue tasks and expires lapsed delegations; returns how many rows changed.</summary>
    public Task<int> RunAsync(int batchSize, CancellationToken cancellationToken);
}
