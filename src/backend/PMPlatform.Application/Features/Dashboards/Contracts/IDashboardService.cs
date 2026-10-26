using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards.Contracts;

/// <summary>
/// FG-01 at runtime (TASK-069): the dashboards a person may open, each read in its context, and ADR-019's personalisation. A role
/// selects a dashboard and never grants data: every widget is authorised on its own source permission and the caller's scope before
/// anything is counted (DSH-CC-02 to -05), and a context the caller may not see is one that does not exist (R-47).
/// </summary>
public interface IDashboardService
{
    /// <summary>The PUBLISHED dashboards the caller's roles are an audience of, by code, with the one they land on (FG-01 §5.1).</summary>
    public Task<DashboardCataloguePage> ListAsync(Guid callerId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The dashboard's PUBLISHED version with each widget's result. A dashboard outside the caller's audience is 404.</summary>
    public Task<AdministrationResult<DashboardView>> GetAsync(Guid callerId, DashboardCode code, DashboardContext context, CancellationToken cancellationToken);

    /// <summary>Replaces the caller's choices for the optional widgets of the dashboard's PUBLISHED version (ADR-019, LAYOUT_PERSONALIZE).</summary>
    public Task<AdministrationResult<DashboardPersonalizationDetail>> PersonalizeAsync(
        Guid callerId, DashboardCode code, DashboardPersonalizationInput input, CancellationToken cancellationToken);

    /// <summary>Back to the governed layout: the caller's preference is deleted (HARD_OWNER). One that is not there is not an error.</summary>
    public Task<AdministrationResult<DashboardPersonalizationDetail>> ResetPersonalizationAsync(Guid callerId, DashboardCode code, CancellationToken cancellationToken);
}
