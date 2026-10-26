using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Project;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// What a change request may name and when its project admits it: the requested governance profile (MasterDataConfig, E-U2), the
/// project's lifecycle state (Project, edge 6), and what each change type needs before it is submitted.
/// </summary>
internal sealed class ChangeRequestReferences(IMasterDataResolver masterData)
{
    /// <summary>A change request is raised, edited, submitted and reviewed once the project is approved and until it completes.</summary>
    public static AdministrationError? RaisingRefused(ProjectFacts project) =>
        project.Status is ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active or ProjectLifecycleState.Suspended
            ? null
            : AdministrationError.Rule(ChangeRequestErrorCodes.ProjectNotEligible);

    /// <summary>Implementation opens only while the project may be changed: not while it is SUSPENDED (WF-08 BR-CHG-040), nor once it completes.</summary>
    public static AdministrationError? ImplementationRefused(ProjectFacts project) =>
        project.Status is ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active
            ? null
            : AdministrationError.Rule(ChangeRequestErrorCodes.ProjectNotEligible);

    /// <summary>An implemented request is marked and closed in those states and once the project is COMPLETED, so it is closed out before the project.</summary>
    public static AdministrationError? FinishingRefused(ProjectFacts project) =>
        project.Status == ProjectLifecycleState.Completed ? null : RaisingRefused(project);

    /// <summary>
    /// What its change type needs to be submitted (WF-08 VAL-CHG-003, VAL-CHG-005): a schedule change its days, a cost change its amount,
    /// a scope change its scope impact, a contractual obligation the flag, a governance-profile change the profile asked for. Every miss
    /// is a field issue.
    /// </summary>
    public static AdministrationError? IncompleteRefusal(ChangeRequestEntity request)
    {
        ArgumentNullException.ThrowIfNull(request);
        FieldIssue? missing = request.ChangeType switch
        {
            ChangeType.Schedule when request.ScheduleImpactDays is null or 0 => new FieldIssue("scheduleImpactDays", FieldIssue.Required),
            ChangeType.Cost when request.CostImpactSar is null || request.CostImpactSar.Value.Amount == 0 => new FieldIssue("costImpactSar", FieldIssue.Required),
            ChangeType.Scope when request.ScopeImpact is null => new FieldIssue("scopeImpact", FieldIssue.Required),
            ChangeType.ContractualObligation when !request.IsContractualObligation => new FieldIssue("isContractualObligation", FieldIssue.Required),
            ChangeType.GovernanceProfile when request.RequestedGovernanceProfileItemId is null => new FieldIssue("requestedGovernanceProfileItemId", FieldIssue.Required),
            _ => null,
        };
        return missing is null ? null : AdministrationError.Rule(ChangeRequestErrorCodes.Incomplete, missing);
    }

    /// <summary>A requested governance profile, when given, is a PUBLISHED GOVERNANCE_PROFILE item other than the project's own (TASK-105).</summary>
    public async Task<AdministrationError?> ProfileRefusedAsync(ProjectFacts project, Guid? requestedProfileItemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (requestedProfileItemId is not { } requested)
        {
            return null;
        }

        if (requested == project.GovernanceProfileItemId)
        {
            return AdministrationError.Rule(ChangeRequestErrorCodes.ProfileInvalid, new FieldIssue("requestedGovernanceProfileItemId", FieldIssue.NotAllowed));
        }

        try
        {
            await masterData.RequirePublishedItemAsync(MasterDataCatalogueCodes.GovernanceProfile, requested, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (ConfigurationMissingException)
        {
            return AdministrationError.Rule(ChangeRequestErrorCodes.ProfileInvalid, new FieldIssue("requestedGovernanceProfileItemId", FieldIssue.NotFound));
        }
    }
}
