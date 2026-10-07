using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ManagementConcern;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// How a concern comes to exist, whoever raises it — a person through the API, or WF-06 materialising a risk (edge 15): the rules it
/// must meet, its computed severity, its first review date, and the rows and audit event staged in the caller's unit of work. Nothing
/// is saved here.
/// </summary>
internal sealed class ConcernIntake(IManagementConcernRepository repository, ConcernReferences references, ConcernSeverity severity, IAuditTrail audit)
{
    public async Task<AdministrationResult<ConcernEntity>> StageAsync(
        Guid actorId, ProjectFacts project, ConcernDraft draft, Guid? originatingRiskId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(draft);

        AdministrationError? broken = ConcernReferences.RaisingRefused(project)
                                      ?? ConcernReferences.TargetDateRefused(draft.TargetResolutionDate, now)
                                      ?? await references.CategoryRefusedAsync(draft.CategoryItemId, cancellationToken).ConfigureAwait(false)
                                      ?? await references.PriorityRefusedAsync(draft.PriorityItemId, cancellationToken).ConfigureAwait(false);
        if (broken is not null)
        {
            return broken;
        }

        ComputedSeverity? computed = null;
        if (draft.Impacts.Count > 0)
        {
            AdministrationResult<ComputedSeverity> result = await severity.ComputeAsync(draft.Impacts, now, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return result.Error;
            }

            computed = result.Value;
        }

        ConcernEntity concern = new()
        {
            Id = Guid.CreateVersion7(now),
            ProjectId = project.Id,
            ConcernType = draft.ConcernType,
            Title = draft.Title,
            Description = draft.Description,
            CategoryItemId = draft.CategoryItemId,
            PriorityItemId = draft.PriorityItemId,
            Status = ConcernStatus.Open,
            RaisedByUserId = actorId,
            RaisedAt = now,
            OriginatingRiskId = originatingRiskId,
            TargetResolutionDate = draft.TargetResolutionDate,
            NextReviewDate = await references.NextReviewDateAsync(project, now, cancellationToken).ConfigureAwait(false),
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        repository.Add(concern);
        if (computed is not null)
        {
            Apply(concern, computed, draft.Impacts, actorId, now);
            if (draft.ClaimedSeverityItemId is { } claimed)
            {
                concern.SeverityItemId = claimed;
            }
        }

        audit.Stage(ConcernAudit.Raised(actorId, project, concern));
        return concern;
    }

    /// <summary>Gives the concern <paramref name="impacts"/> and the severity computed from them. Its earlier impacts are removed by the caller.</summary>
    public void Apply(ConcernEntity concern, ComputedSeverity computed, IReadOnlyList<ConcernImpactInput> impacts, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(concern);
        ArgumentNullException.ThrowIfNull(computed);
        ArgumentNullException.ThrowIfNull(impacts);
        concern.OverallImpactLevel = computed.OverallImpactLevel;
        concern.SeverityItemId = computed.SeverityItemId;
        concern.SeverityConfigurationVersionId = computed.ConfigurationVersionId;
        foreach (ConcernImpactInput impact in impacts)
        {
            repository.Add(new ConcernImpact
            {
                Id = Guid.CreateVersion7(now),
                ManagementConcernId = concern.Id,
                ImpactDimensionItemId = impact.ImpactDimensionItemId,
                ImpactLevel = impact.ImpactLevel,
                Rationale = impact.Rationale,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }
    }
}
