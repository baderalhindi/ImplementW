using PMPlatform.Application.Features.Approval;
using PMPlatform.Domain.Approval;

namespace PMPlatform.Tests.Unit.Application.Approval;

/// <summary>The delegations authority is resolved against; the rest of the repository is not used by these tests.</summary>
internal sealed class FakeApprovalRepository : IApprovalRepository
{
    public List<ApprovalDelegation> Delegations { get; } = [];

    public Task<IReadOnlyList<ApprovalDelegation>> FindDelegationsToAsync(Guid delegateUserId, DateTimeOffset at, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ApprovalDelegation>>([.. Delegations
            .Where(d => d.DelegateUserId == delegateUserId && d.Status == ApprovalDelegationStatus.Active && d.ValidFrom <= at && d.ValidTo > at)
            .OrderBy(d => d.ValidFrom)]);

    public Task<ApprovalInstance?> FindInstanceAsync(Guid instanceId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<ApprovalTask>> GetTasksAsync(Guid instanceId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ApprovalTask?> FindTaskAsync(Guid taskId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<ApprovalRun>> FindBySubjectAsync(string subjectModule, string subjectType, Guid subjectId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<(IReadOnlyList<ApprovalInstance> Items, int TotalCount)> ListRequestedByAsync(
        Guid userId, IReadOnlyCollection<ApprovalInstanceStatus> statuses, int skip, int take, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<(ApprovalTask Task, ApprovalInstance Instance)>> FindCurrentTasksAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Guid>> FindOverdueTaskIdsAsync(DateTimeOffset at, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<ApprovalDelegation>> ListDelegationsAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ApprovalDelegation?> FindDelegationAsync(Guid delegationId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<ApprovalDelegation>> FindLapsedDelegationsAsync(DateTimeOffset at, int count, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public void Add(ApprovalInstance instance) => throw new NotSupportedException();

    public void Add(ApprovalTask task) => throw new NotSupportedException();

    public void Add(ApprovalDelegation delegation) => throw new NotSupportedException();

    public Task<ApprovalSaveOutcome> SaveAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}
