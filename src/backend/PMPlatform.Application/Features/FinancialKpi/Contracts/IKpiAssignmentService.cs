using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>KPI Performance (TASK-052): the catalogue KPIs assigned to a project, and portfolio aggregates of their measurements.</summary>
public interface IKpiAssignmentService
{
    public Task<KpiAssignmentPage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiAssignmentDetail>>> GetAsync(Guid callerId, Guid assignmentId, CancellationToken cancellationToken);

    /// <summary>Assigns a PUBLISHED KPI definition to the project, ACTIVE.</summary>
    public Task<AdministrationResult<Versioned<KpiAssignmentDetail>>> CreateAsync(Guid callerId, KpiAssignmentDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiAssignmentDetail>>> UpdateAsync(
        Guid callerId, Guid assignmentId, KpiAssignmentChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>ACTIVE ↔ SUSPENDED, and either → RETIRED, which is final.</summary>
    public Task<AdministrationResult<Versioned<KpiAssignmentDetail>>> TransitionAsync(
        Guid callerId, Guid assignmentId, Domain.FinancialKpi.KpiAssignmentStatus status, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The latest published measurement of each KPI named on each project named, combined only within one unit.</summary>
    public Task<KpiPortfolioAggregate> AggregateAsync(
        Guid callerId, IReadOnlyList<Guid> kpiDefinitionIds, IReadOnlyList<Guid> projectIds, CancellationToken cancellationToken);
}
