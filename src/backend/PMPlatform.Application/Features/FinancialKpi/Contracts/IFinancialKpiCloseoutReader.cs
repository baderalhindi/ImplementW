namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 14, Closure → FinancialKpi (query; TASK-063): closure readiness against the project's financial and KPI state. WF-10
/// reads WF-14's own statuses and is no ledger or KPI engine (BR-CLO-030). It authorizes no one.
/// </summary>
public interface IFinancialKpiCloseoutReader
{
    public Task<FinancialKpiCloseoutPosition> ReadAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <param name="Unpublished">Financial progress updates and KPI measurements not yet PUBLISHED.</param>
/// <param name="OpenVersions">Budget and KPI target versions not yet ACTIVE, SUPERSEDED, REJECTED or WITHDRAWN.</param>
public sealed record FinancialKpiCloseoutPosition(int Unpublished, int OpenVersions);
