using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Domain.ManagementConcern;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// The representations of WF-07's rows: a concern with its impacts and its OPEN escalation, read for a page at once; an escalation with the
/// role it is addressed to.
/// </summary>
internal sealed class ConcernViews(IManagementConcernRepository repository, IRoleDirectory roles)
{
    public async Task<ConcernDetail> DetailAsync(ConcernEntity concern, CancellationToken cancellationToken) =>
        (await DetailsAsync([concern], cancellationToken).ConfigureAwait(false))[0];

    public async Task<IReadOnlyList<ConcernDetail>> DetailsAsync(IReadOnlyList<ConcernEntity> concerns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(concerns);
        Guid[] ids = [.. concerns.Select(c => c.Id)];
        ILookup<Guid, ConcernImpact> impacts = (await repository.ListImpactsAsync(ids, cancellationToken).ConfigureAwait(false)).ToLookup(i => i.ManagementConcernId);
        Dictionary<Guid, ConcernEscalationDetail> open = (await EscalationsAsync(
                await repository.ListOpenEscalationsAsync(ids, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false))
            .ToDictionary(e => e.ManagementConcernId);
        return
        [
            .. concerns.Select(c => new ConcernDetail(
                c.Id, c.ProjectId, c.ConcernType, c.Title, c.Description, c.CategoryItemId, c.PriorityItemId, c.OverallImpactLevel, c.SeverityItemId,
                c.SeverityConfigurationVersionId,
                [.. impacts[c.Id].OrderBy(i => i.ImpactDimensionItemId).Select(i => new ConcernImpactDetail(i.ImpactDimensionItemId, i.ImpactLevel, i.Rationale))],
                c.Status, c.RevisionNo, c.RaisedByUserId, c.RaisedAt, c.AssigneeUserId, c.OriginatingRiskId, c.TargetResolutionDate, c.NextReviewDate, c.LastReviewedAt,
                c.Resolution, c.ResolvedAt, c.ClosedAt, open.GetValueOrDefault(c.Id),
                c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy)),
        ];
    }

    public async Task<ConcernEscalationDetail> EscalationAsync(ConcernEscalation escalation, CancellationToken cancellationToken) =>
        (await EscalationsAsync([escalation], cancellationToken).ConfigureAwait(false))[0];

    public async Task<IReadOnlyList<ConcernEscalationDetail>> EscalationsAsync(IReadOnlyList<ConcernEscalation> escalations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(escalations);
        if (escalations.Count == 0)
        {
            return [];
        }

        // A role is never deleted (deactivated at most), so every escalation's role is listed.
        Dictionary<Guid, RoleSummary> byId = (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Id);
        return [.. escalations.Select(e => ToDetail(e, byId[e.EscalatedToRoleId]))];
    }

    private static ConcernEscalationDetail ToDetail(ConcernEscalation e, RoleSummary role) =>
        new(e.Id, e.ManagementConcernId, e.EscalationNo, e.EscalatedByUserId, e.EscalatedAt, e.EscalatedToRoleId, role.Code, role.Name, e.Reason, e.Status, e.ResolvedAt,
            e.ResolvedByUserId, e.Resolution, e.UpdatedAt);
}
