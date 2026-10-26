using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// An assignment's periodic measurements (TASK-052). Each is pinned, when recorded, to the ACTIVE target version and rated
/// against it; the pin and the rating are never rewritten by a later target. Published by AHDA.
/// </summary>
public interface IKpiMeasurementService
{
    public Task<KpiMeasurementPage> ListAsync(Guid callerId, Guid assignmentId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> GetAsync(Guid callerId, Guid measurementId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> CreateAsync(Guid callerId, KpiMeasurementDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> UpdateAsync(
        Guid callerId, Guid measurementId, KpiMeasurementChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid measurementId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> SubmitAsync(Guid callerId, Guid measurementId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED → PUBLISHED: AHDA's gate, internal users only and never the person who recorded it (ADR-013).</summary>
    public Task<AdministrationResult<Versioned<KpiMeasurementDetail>>> PublishAsync(Guid callerId, Guid measurementId, uint? expectedVersion, CancellationToken cancellationToken);
}
