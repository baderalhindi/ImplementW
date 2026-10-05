using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 15, Risk → ManagementConcern (command): WF-06 has WF-07 raise an issue from a risk that has occurred.
/// The issue records <see cref="RiskIssueCommand.OriginatingRiskId"/> as <c>management_concern.originating_risk_id</c>, the
/// one foreign key both sides query (ERD §5.10). WF-07 is not built (TASK-057), so the only implementation until then is
/// <see cref="ManagementConcern.UnbuiltIssueRegister"/>, which refuses (risk-management.md F-1).
/// </summary>
public interface IRiskIssueMaterialisation
{
    /// <summary>
    /// Raises an ISSUE on the risk's project, in the caller's unit of work, so the issue and the risk's materialisation commit
    /// together (ADR-003 M-11). WF-07 decides its own rules — category, priority, severity — and refuses with its own codes.
    /// </summary>
    public Task<AdministrationResult<OriginatedIssue>> RaiseIssueAsync(RiskIssueCommand command, CancellationToken cancellationToken);

    /// <summary>The issues raised from any of the risks, oldest first: the risk side of the linkage, for a page of risks at once.</summary>
    public Task<IReadOnlyList<OriginatedIssue>> ListByOriginatingRisksAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken);
}

/// <summary>
/// An issue to raise from a risk. The impacts are the risk's latest assessment's, on the dimension set risks and issues share
/// (ADR-011); empty for a risk never assessed. WF-07 computes severity from them; nothing here is a severity.
/// </summary>
public sealed record RiskIssueCommand(
    Guid ActorUserId,
    Guid ProjectId,
    Guid OriginatingRiskId,
    NarrativeText Title,
    NarrativeText Description,
    Guid CategoryItemId,
    Guid PriorityItemId,
    IReadOnlyList<RiskIssueImpact> Impacts,
    DateTimeOffset RaisedAt);

public sealed record RiskIssueImpact(Guid ImpactDimensionItemId, short ImpactLevel);

/// <summary>An issue raised from a risk, as WF-07 holds it.</summary>
public sealed record OriginatedIssue(Guid Id, Guid ProjectId, Guid OriginatingRiskId, string Status, Guid RaisedByUserId, DateTimeOffset RaisedAt);
