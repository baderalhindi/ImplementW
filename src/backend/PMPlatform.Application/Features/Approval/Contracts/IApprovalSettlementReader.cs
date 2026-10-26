namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 27, Closure → Approval (query; TASK-063): whether any WF-11 decision about a project is still to land on its source
/// module. WF-10 moves a project only when none is, so no outcome reaches a record after its project completes or closes.
/// </summary>
public interface IApprovalSettlementReader
{
    /// <summary>The runs scoped to the project that are PENDING, or decided and not yet delivered to their source module.</summary>
    public Task<int> CountUnsettledAsync(Guid scopeProjectId, CancellationToken cancellationToken);
}
