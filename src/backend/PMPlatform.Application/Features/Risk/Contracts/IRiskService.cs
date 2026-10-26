using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Risk.Contracts;

/// <summary>
/// WF-06's risk register (TASK-055): risks — each naming the issues raised from it — their assessment versions and acceptances. Each
/// operation is decided by the authorization engine on the project's anchors. A collection is of one risk or one project and
/// is empty for one the caller may not see; a risk the caller may not see is 404 (R-47).
/// </summary>
public interface IRiskService
{
    public Task<RiskPage> ListRisksAsync(Guid callerId, RiskQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<RiskDetail>>> GetRiskAsync(Guid callerId, Guid riskId, CancellationToken cancellationToken);

    /// <summary>
    /// Registers a risk, IDENTIFIED, while the project is APPROVED_PLANNED, ACTIVE or SUSPENDED and its governance profile carries
    /// risk management (ADR-015). RISK_MANAGE; ADR-013 lets an entity Project Manager register risks on their own project.
    /// </summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> RegisterRiskAsync(Guid callerId, RiskDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces an open risk's register fields. Requires the caller's version (R-21). RISK_MANAGE.</summary>
    public Task<AdministrationResult<Versioned<RiskDetail>>> UpdateRiskAsync(
        Guid callerId, Guid riskId, RiskChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>The risk's assessment versions, newest first, each with the rating its pinned matrix version gave it.</summary>
    public Task<RiskAssessmentPage> ListAssessmentsAsync(Guid callerId, Guid riskId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<RiskAssessmentDetail>> GetAssessmentAsync(Guid callerId, Guid assessmentId, CancellationToken cancellationToken);

    public Task<RiskAcceptancePage> ListAcceptancesAsync(Guid callerId, Guid riskId, PageRequest page, CancellationToken cancellationToken);
}
