using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>The KPI catalogue (TASK-034): KPI identities under the governed lifecycle, which KPI_POLICY versions reference.</summary>
public interface IKpiDefinitionAdministrationService
{
    public Task<AdministrationResult<KpiDefinitionPage>> ListAsync(KpiDefinitionQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> GetAsync(Guid kpiDefinitionId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> CreateAsync(Guid actorId, KpiDefinitionDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> UpdateAsync(
        Guid actorId, Guid kpiDefinitionId, KpiDefinitionChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> ValidateAsync(Guid actorId, Guid kpiDefinitionId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> PublishAsync(Guid actorId, Guid kpiDefinitionId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiDefinitionDetail>>> RetireAsync(Guid actorId, Guid kpiDefinitionId, uint? expectedVersion, CancellationToken cancellationToken);
}
