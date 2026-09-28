using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval;

/// <summary>The <c>approval</c> schema. Finds that return rows to change track them; the others do not.</summary>
public interface IApprovalRepository
{
    /// <summary>Tracked.</summary>
    public Task<ApprovalInstance?> FindInstanceAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>The run's tasks, tracked, in stage order and then in the order they were created.</summary>
    public Task<IReadOnlyList<ApprovalTask>> GetTasksAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<ApprovalTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>Every run of the subject, oldest revision first, with its tasks. Not tracked.</summary>
    public Task<IReadOnlyList<ApprovalRun>> FindBySubjectAsync(string subjectModule, string subjectType, Guid subjectId, CancellationToken cancellationToken);

    /// <summary>The runs <paramref name="userId"/> requested, newest first, one page, with the total count. Not tracked.</summary>
    public Task<(IReadOnlyList<ApprovalInstance> Items, int TotalCount)> ListRequestedByAsync(
        Guid userId, IReadOnlyCollection<ApprovalInstanceStatus> statuses, int skip, int take, CancellationToken cancellationToken);

    /// <summary>
    /// PENDING tasks of PENDING runs in their run's current stage (no PENDING task of the run has a lower sequence),
    /// assigned to one of <paramref name="roleIds"/>, with their runs. Not tracked.
    /// </summary>
    public Task<IReadOnlyList<(ApprovalTask Task, ApprovalInstance Instance)>> FindCurrentTasksAsync(
        IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);

    /// <summary>PENDING tasks of PENDING runs due at or before <paramref name="at"/> that no escalation created, oldest due first. Not tracked.</summary>
    public Task<IReadOnlyList<Guid>> FindOverdueTaskIdsAsync(DateTimeOffset at, int count, CancellationToken cancellationToken);

    /// <summary>ACTIVE delegations to <paramref name="delegateUserId"/> whose period contains <paramref name="at"/>, oldest first. Not tracked.</summary>
    public Task<IReadOnlyList<ApprovalDelegation>> FindDelegationsToAsync(Guid delegateUserId, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Delegations <paramref name="userId"/> gave or received, newest first. Not tracked.</summary>
    public Task<IReadOnlyList<ApprovalDelegation>> ListDelegationsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<ApprovalDelegation?> FindDelegationAsync(Guid delegationId, CancellationToken cancellationToken);

    /// <summary>ACTIVE delegations whose period ended at or before <paramref name="at"/>. Tracked.</summary>
    public Task<IReadOnlyList<ApprovalDelegation>> FindLapsedDelegationsAsync(DateTimeOffset at, int count, CancellationToken cancellationToken);

    public void Add(ApprovalInstance instance);

    public void Add(ApprovalTask task);

    public void Add(ApprovalDelegation delegation);

    /// <summary>
    /// Saves the tracked changes. A run changed by another decision since it was read, or a subject revision that
    /// already has a run, is an answer, not a fault; after either nothing stays tracked.
    /// </summary>
    public Task<ApprovalSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}
