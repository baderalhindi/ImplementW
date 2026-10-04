using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// Financial Progress (TASK-052): periodic actuals and forecast for WF-02's reporting periods (edge 13), reviewed and published
/// by AHDA into immutable Published Financial Snapshots; the CURRENT/LIVE position beside them, never merged with them; and
/// portfolio totals that add only verified SAR figures.
/// </summary>
public interface IFinancialProgressService
{
    public Task<FinancialProgressUpdatePage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> GetAsync(Guid callerId, Guid updateId, CancellationToken cancellationToken);

    /// <summary>Opens the earliest begun period without published figures as a DRAFT, every figure Unknown (MISSING).</summary>
    public Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> StartAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> UpdateAsync(
        Guid callerId, Guid updateId, FinancialProgressUpdateChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>HARD_DRAFT. One that is not there is not an error (R-40).</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> SubmitAsync(Guid callerId, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> StartReviewAsync(Guid callerId, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>UNDER_REVIEW → RETURNED with the reason; the period continues as revision + 1, a DRAFT with this one's figures.</summary>
    public Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> ReturnAsync(
        Guid callerId, Guid updateId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>UNDER_REVIEW → PUBLISHED, writing the period's snapshot. No earlier snapshot is touched.</summary>
    public Task<AdministrationResult<Versioned<FinancialProgressUpdateDetail>>> PublishAsync(Guid callerId, Guid updateId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<PublishedFinancialSnapshotPage> ListSnapshotsAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<FinancialPositionPage> ListPositionsAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Totals over the projects named, from their latest snapshots or their live positions.</summary>
    public Task<FinancialPortfolioAggregate> AggregateAsync(Guid callerId, IReadOnlyList<Guid> projectIds, SemanticState semanticState, CancellationToken cancellationToken);
}
