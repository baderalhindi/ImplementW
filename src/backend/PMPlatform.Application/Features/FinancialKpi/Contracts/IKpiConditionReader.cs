using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 29, Dashboards → FinancialKpi (read projection; TASK-069): the KPI Condition of each ACTIVE assignment as WF-14
/// rated its latest published measurement. A condition is counted, never averaged across unlike KPIs (BR-DSH-022). It authorizes
/// no one; the RAG rating is not a classified field (the measured value is, and is not read here).
/// </summary>
public interface IKpiConditionReader
{
    public Task<IReadOnlyList<KpiCondition>> ListAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);
}

/// <summary>
/// One ACTIVE assignment's latest published measurement: its RAG as rated against its pinned target, and whether it carries a
/// figure. <see cref="RagStatus"/> and the rest are null while nothing has been published for the assignment.
/// </summary>
public sealed record KpiCondition(
    Guid ProjectId, Guid KpiAssignmentId, Guid KpiDefinitionId, KpiRagStatus? RagStatus, ValueStatus? ValueStatus, DateOnly? AsOfDate, DateTimeOffset? PublishedAt);
