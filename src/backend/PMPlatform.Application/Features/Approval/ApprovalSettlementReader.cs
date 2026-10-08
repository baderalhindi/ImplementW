using PMPlatform.Application.Features.Approval.Contracts;

namespace PMPlatform.Application.Features.Approval;

/// <summary>The project's WF-11 runs whose decision has yet to land on its source, for WF-10 (edge 27).</summary>
internal sealed class ApprovalSettlementReader(IApprovalRepository repository) : IApprovalSettlementReader
{
    public Task<int> CountUnsettledAsync(Guid scopeProjectId, CancellationToken cancellationToken) => repository.CountUnsettledAsync(scopeProjectId, cancellationToken);
}
