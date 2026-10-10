using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>
/// SCR-139 Saved Reports / Views (FG-02 §10.2; ADR-019): a person's private configurations, which no one else reads — another person's view
/// does not exist for them (R-47). Every read and run checks a view against the report version and the allowlist in force, and every run
/// against its owner's access then (BR-RPT-033). Under <c>REPORT_COMPOSE</c>.
/// </summary>
public interface ISavedViewService
{
    public Task<SavedViewPage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<SavedViewDetail>>> GetAsync(Guid callerId, Guid savedViewId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<SavedViewDetail>>> CreateAsync(Guid callerId, SavedViewInput input, CancellationToken cancellationToken);

    /// <summary>Replaces the view, whole, by its owner. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<SavedViewDetail>>> UpdateAsync(Guid callerId, Guid savedViewId, SavedViewInput input, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>HARD_OWNER: the owner deletes their view. Null when deleted; NOT_FOUND for a view that is not theirs or no longer exists (R-40, R-47).</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid savedViewId, CancellationToken cancellationToken);

    /// <summary>Runs the view now, as its owner may see now (RPT-API-023).</summary>
    public Task<AdministrationResult<ReportResultPage>> RunAsync(Guid callerId, Guid savedViewId, PageRequest page, CancellationToken cancellationToken);
}
