using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// An assignment's target versions (TASK-052): opened DRAFT, submitted to WF-11 (edge 26), ACTIVE on approval and immutable from
/// then on. A new target is a new version; the one it supersedes is kept, and so is every measurement pinned to it.
/// </summary>
public interface IKpiTargetVersionService
{
    public Task<KpiTargetVersionPage> ListAsync(Guid callerId, Guid assignmentId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiTargetVersionDetail>>> GetAsync(Guid callerId, Guid targetVersionId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiTargetVersionDetail>>> CreateAsync(Guid callerId, KpiTargetVersionDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiTargetVersionDetail>>> UpdateAsync(
        Guid callerId, Guid targetVersionId, KpiTargetVersionChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid targetVersionId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiTargetVersionDetail>>> SubmitAsync(Guid callerId, Guid targetVersionId, uint? expectedVersion, CancellationToken cancellationToken);
}
