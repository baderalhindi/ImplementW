using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ManagementConcern;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// WF-07's side of edge 15 (<see cref="IRiskIssueMaterialisation"/>): an ISSUE raised from a risk, staged in the risk's unit of work so
/// the issue and the risk's materialisation commit together (M-11), holding the risk as <c>originating_risk_id</c>. WF-06 authorised
/// the command; WF-07 applies its own rules and refuses with its own codes. The risk's impacts are the issue's first assessment when
/// they fit the scale in force — its severity is still computed by WF-07's rule, never copied from the risk's rating (BR-ISS-028) — and
/// the issue is raised unassessed when they do not.
/// </summary>
internal sealed class RiskIssueRegister(IManagementConcernRepository repository, IProjectFactsReader projects, ConcernIntake intake) : IRiskIssueMaterialisation
{
    public async Task<AdministrationResult<OriginatedIssue>> RaiseIssueAsync(RiskIssueCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ProjectFacts project = await projects.FindAsync(command.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Risk {command.OriginatingRiskId} names no project.");

        ConcernImpactInput[] impacts = [.. command.Impacts.Select(i => new ConcernImpactInput(i.ImpactDimensionItemId, i.ImpactLevel, null))];
        AdministrationResult<ConcernEntity> raised = await RaiseAsync(command, project, impacts, cancellationToken).ConfigureAwait(false);
        if (!raised.Succeeded && raised.Error.Code == ConcernErrorCodes.ImpactInvalid)
        {
            raised = await RaiseAsync(command, project, [], cancellationToken).ConfigureAwait(false);
        }

        return raised.Succeeded ? ToOriginated(raised.Value) : raised.Error;
    }

    public async Task<IReadOnlyList<OriginatedIssue>> ListByOriginatingRisksAsync(IReadOnlyCollection<Guid> riskIds, CancellationToken cancellationToken) =>
        [.. (await repository.ListByOriginatingRisksAsync(riskIds, cancellationToken).ConfigureAwait(false)).Select(ToOriginated)];

    private Task<AdministrationResult<ConcernEntity>> RaiseAsync(RiskIssueCommand command, ProjectFacts project, IReadOnlyList<ConcernImpactInput> impacts, CancellationToken cancellationToken) =>
        intake.StageAsync(
            command.ActorUserId, project,
            new ConcernDraft(project.Id, ConcernType.Issue, command.Title, command.Description, command.CategoryItemId, command.PriorityItemId, impacts, null),
            command.OriginatingRiskId, command.RaisedAt, cancellationToken);

    private static OriginatedIssue ToOriginated(ConcernEntity c) =>
        new(c.Id, c.ProjectId, c.OriginatingRiskId!.Value, System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(c.Status.ToString()), c.RaisedByUserId, c.RaisedAt);
}
