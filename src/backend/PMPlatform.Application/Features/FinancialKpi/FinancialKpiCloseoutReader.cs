using PMPlatform.Application.Features.FinancialKpi.Contracts;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>A project's unpublished figures and undecided versions, for WF-10's readiness (edge 14).</summary>
internal sealed class FinancialKpiCloseoutReader(IFinancialKpiRepository repository) : IFinancialKpiCloseoutReader
{
    public async Task<FinancialKpiCloseoutPosition> ReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        (int unpublished, int openVersions) = await repository.CountUnsettledAsync(projectId, cancellationToken).ConfigureAwait(false);
        return new FinancialKpiCloseoutPosition(unpublished, openVersions);
    }
}
