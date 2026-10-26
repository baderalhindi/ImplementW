using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Application.Features.Reports.Contracts;

/// <summary>
/// ADM-037 Report Configuration (FG-02 §11; TASK-071) under the platform's governed lifecycle, as ADM-036 is: a new version is a DRAFT edited by
/// its author; a reviewer validates it and a publisher publishes it, each running the full validation again; publishing retires the version it
/// replaces. The catalogue is the ten reports of ADR-006: a version is of one of them, and a PUBLISHED one is superseded, never removed.
/// Configuring a report grants no business data (BR-RPT-048).
/// </summary>
public interface IReportDefinitionService
{
    public Task<ReportDefinitionPage> ListAsync(ReportDefinitionQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> GetAsync(Guid definitionId, CancellationToken cancellationToken);

    /// <summary>Opens the report's next version as a DRAFT, a copy of its PUBLISHED version. One version is on its way at a time.</summary>
    public Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> CreateAsync(Guid actorId, ReportCode code, CancellationToken cancellationToken);

    /// <summary>Replaces a DRAFT's content, as a whole, by its author. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> UpdateAsync(
        Guid actorId, Guid definitionId, ReportDefinitionContent content, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>DRAFT → VALIDATED by a reviewer who is not the author, once FG-02 §11.2's validation passes.</summary>
    public Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> ValidateAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>VALIDATED → PUBLISHED by a third person, validated again; the version it replaces is retired in the same save.</summary>
    public Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> PublishAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>A DRAFT or VALIDATED version abandoned → RETIRED. A PUBLISHED one is replaced, not retired (409 <c>REPORT_RETIREMENT_NOT_PERMITTED</c>).</summary>
    public Task<AdministrationResult<Versioned<ReportDefinitionDetail>>> RetireAsync(Guid actorId, Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken);
}
