using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Domain.ManagementConcern;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>The representations of WF-07's rows: a concern with its impacts and its OPEN escalation, read for a page at once.</summary>
internal sealed class ConcernViews(IManagementConcernRepository repository)
{
    public async Task<ConcernDetail> DetailAsync(ConcernEntity concern, CancellationToken cancellationToken) =>
        (await DetailsAsync([concern], cancellationToken).ConfigureAwait(false))[0];

    public async Task<IReadOnlyList<ConcernDetail>> DetailsAsync(IReadOnlyList<ConcernEntity> concerns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(concerns);
        Guid[] ids = [.. concerns.Select(c => c.Id)];
        ILookup<Guid, ConcernImpact> impacts = (await repository.ListImpactsAsync(ids, cancellationToken).ConfigureAwait(false)).ToLookup(i => i.ManagementConcernId);
        Dictionary<Guid, ConcernEscalation> open = (await repository.ListOpenEscalationsAsync(ids, cancellationToken).ConfigureAwait(false)).ToDictionary(e => e.ManagementConcernId);
        return
        [
            .. concerns.Select(c => new ConcernDetail(
                c.Id, c.ProjectId, c.ConcernType, c.Title, c.Description, c.CategoryItemId, c.PriorityItemId, c.OverallImpactLevel, c.SeverityItemId,
                c.SeverityConfigurationVersionId,
                [.. impacts[c.Id].OrderBy(i => i.ImpactDimensionItemId).Select(i => new ConcernImpactDetail(i.ImpactDimensionItemId, i.ImpactLevel, i.Rationale))],
                c.Status, c.RevisionNo, c.RaisedByUserId, c.RaisedAt, c.AssigneeUserId, c.OriginatingRiskId, c.TargetResolutionDate, c.NextReviewDate, c.LastReviewedAt,
                c.Resolution, c.ResolvedAt, c.ClosedAt, open.TryGetValue(c.Id, out ConcernEscalation? escalation) ? ToDetail(escalation) : null,
                c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy)),
        ];
    }

    public static ConcernEscalationDetail ToDetail(ConcernEscalation e) =>
        new(e.Id, e.ManagementConcernId, e.EscalationNo, e.EscalatedByUserId, e.EscalatedAt, e.EscalatedToRoleId, e.Reason, e.Status, e.ResolvedAt, e.ResolvedByUserId,
            e.Resolution, e.UpdatedAt);
}
