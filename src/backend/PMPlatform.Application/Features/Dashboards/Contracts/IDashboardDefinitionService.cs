using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Dashboards;

namespace PMPlatform.Application.Features.Dashboards.Contracts;

/// <summary>
/// ADM-036 Dashboard Configuration (FG-01 §13, TASK-069) under the platform's governed lifecycle: a new version is a DRAFT edited by its
/// author; a reviewer validates it and a publisher publishes it, each running the full validation again; publishing retires the version
/// it replaces. The catalogue is the three dashboards of ADR-006: a version is of one of them, and a PUBLISHED one is superseded,
/// never removed. Holding the configuration permission grants no business data (BR-DSH-026).
/// </summary>
public interface IDashboardDefinitionService
{
    public Task<DashboardDefinitionPage> ListAsync(DashboardDefinitionQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> GetAsync(Guid definitionId, CancellationToken cancellationToken);

    /// <summary>Opens the dashboard's next version as a DRAFT, a copy of its PUBLISHED version. One version is on its way at a time.</summary>
    public Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> CreateAsync(Guid actorId, DashboardCode code, CancellationToken cancellationToken);

    /// <summary>Replaces a DRAFT's content, as a whole, by its author. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> UpdateAsync(
        Guid actorId, Guid definitionId, DashboardDefinitionContent content, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>DRAFT → VALIDATED by a reviewer who is not the author, once FG-01 §13.4's validation passes.</summary>
    public Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> ValidateAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>VALIDATED → PUBLISHED by a third person, validated again; the version it replaces is retired in the same save.</summary>
    public Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> PublishAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>A DRAFT or VALIDATED version abandoned → RETIRED. A PUBLISHED one is replaced, not retired (409 <c>DASHBOARD_RETIREMENT_NOT_PERMITTED</c>).</summary>
    public Task<AdministrationResult<Versioned<DashboardDefinitionDetail>>> RetireAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The registered source projections a widget may bind to (API-DSH-041), by code.</summary>
    public DashboardProjectionPage ListProjections(PageRequest page);
}
