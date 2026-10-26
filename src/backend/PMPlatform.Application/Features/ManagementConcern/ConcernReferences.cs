using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// What a concern may name and when its project admits it: its category and priority (MasterDataConfig, E-U2), its assignee
/// (IdentityAccess, E-U1), the project's lifecycle state (Project, edge 4), and its review cadence from the project's governance
/// profile (ADR-015).
/// </summary>
internal sealed class ConcernReferences(IRoleDirectory roles, IRoleHolderDirectory holders, IMasterDataResolver masterData, IConfigurationResolver configuration)
{
    /// <summary>A concern is raised once the project is approved, and while it runs or is suspended (WF-07 §4.7).</summary>
    public static AdministrationError? RaisingRefused(ProjectFacts project) =>
        project.Status is ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active or ProjectLifecycleState.Suspended
            ? null
            : AdministrationError.Rule(ConcernErrorCodes.ProjectNotEligible);

    /// <summary>A concern is changed in those states and once the project is COMPLETED, so open concerns can be dispositioned before closure.</summary>
    public static AdministrationError? ChangeRefused(ProjectFacts project) =>
        project.Status == ProjectLifecycleState.Completed ? null : RaisingRefused(project);

    /// <summary>A target resolution date, when set or changed, is today or later.</summary>
    public static AdministrationError? TargetDateRefused(DateOnly? targetResolutionDate, DateTimeOffset now) =>
        targetResolutionDate < DateOnly.FromDateTime(now.UtcDateTime)
            ? AdministrationError.Rule(ConcernErrorCodes.TargetDateInvalid, new FieldIssue("targetResolutionDate", FieldIssue.NotAllowed))
            : null;

    public Task<AdministrationError?> CategoryRefusedAsync(Guid categoryItemId, CancellationToken cancellationToken) =>
        ItemRefusedAsync(MasterDataCatalogueCodes.ConcernCategory, categoryItemId, ConcernErrorCodes.CategoryInvalid, "categoryItemId", cancellationToken);

    /// <summary>Priority is chosen by people from the PRIORITY catalogue; the severity never sets it (BR-ISS-011).</summary>
    public Task<AdministrationError?> PriorityRefusedAsync(Guid priorityItemId, CancellationToken cancellationToken) =>
        ItemRefusedAsync(MasterDataCatalogueCodes.Priority, priorityItemId, ConcernErrorCodes.PriorityInvalid, "priorityItemId", cancellationToken);

    /// <summary>An assignee holds some role over the project's anchors now, internal or of the delivering entity, as IdentityAccess decides it.</summary>
    public async Task<AdministrationError?> AssigneeRefusedAsync(ProjectFacts project, Guid assigneeUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        Guid[] roleIds = [.. (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).Select(r => r.Id)];
        RoleHolderScope scope = new(project.Id, project.DepartmentId, project.ExternalEntityId);
        return await holders.FindHolderAsync(assigneeUserId, roleIds, scope, cancellationToken).ConfigureAwait(false) is null
            ? AdministrationError.Rule(ConcernErrorCodes.AssigneeNotEligible, new FieldIssue("assigneeUserId", FieldIssue.NotAllowed))
            : null;
    }

    /// <summary>
    /// ADR-015: a concern's review is due at the cadence of the project's governance profile, counted from <paramref name="now"/>, as
    /// the GOVERNANCE_PROFILE version in force has it; no version fails closed (422 CONFIGURATION_MISSING).
    /// </summary>
    public async Task<DateOnly> NextReviewDateAsync(ProjectFacts project, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ResolvedConfiguration profiles = await configuration.ResolveAsync(ConfigurationFamilyCodes.GovernanceProfile, now, cancellationToken).ConfigureAwait(false);
        return DateOnly.FromDateTime(now.UtcDateTime).AddDays(profiles.RequireGovernanceProfile(project.GovernanceProfileItemId).UpdateCadenceDays);
    }

    private async Task<AdministrationError?> ItemRefusedAsync(string catalogueCode, Guid itemId, string errorCode, string field, CancellationToken cancellationToken)
    {
        try
        {
            await masterData.RequirePublishedItemAsync(catalogueCode, itemId, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (ConfigurationMissingException)
        {
            return AdministrationError.Rule(errorCode, new FieldIssue(field, FieldIssue.NotFound));
        }
    }
}
