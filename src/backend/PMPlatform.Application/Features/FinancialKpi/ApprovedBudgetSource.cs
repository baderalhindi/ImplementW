using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// WF-14 serving WF-08's port (<see cref="IApprovedBudgetSource"/>, edge 12): the project's ACTIVE Approved Budget as a change request's
/// cost is evaluated against it and a COMMITMENT_CHANGE authorisation pins it.
/// </summary>
internal sealed class ApprovedBudgetSource(IFinancialKpiRepository repository) : IApprovedBudgetSource
{
    public async Task<ApprovedBudgetFacts?> FindActiveAsync(Guid projectId, CancellationToken cancellationToken) =>
        await repository.FindActiveCommitmentAsync(projectId, CommitmentType.ApprovedBudget, track: false, cancellationToken).ConfigureAwait(false) is { } active
            ? new ApprovedBudgetFacts(active.Id, active.VersionNo, active.AmountSar)
            : null;
}
