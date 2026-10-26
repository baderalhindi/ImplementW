using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// What a risk may name and when its project admits it: its category (MasterDataConfig, E-U2), its owner and its actions'
/// owners (IdentityAccess, E-U1), the project's lifecycle state (Project, edge 3) and its governance profile (ADR-015).
/// </summary>
internal sealed class RiskReferences(IRoleDirectory roles, IRoleHolderDirectory holders, IMasterDataResolver masterData, IConfigurationResolver configuration)
{
    /// <summary>A risk is registered once the project is approved, and while it runs or is suspended.</summary>
    public static AdministrationError? RegistrationRefused(ProjectFacts project) =>
        project.Status is ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active or ProjectLifecycleState.Suspended
            ? null
            : AdministrationError.Rule(RiskErrorCodes.ProjectNotEligible);

    /// <summary>A risk is changed in those states and once the project is COMPLETED, so its open risks can be closed before closure.</summary>
    public static AdministrationError? ChangeRefused(ProjectFacts project) =>
        project.Status == ProjectLifecycleState.Completed ? null : RegistrationRefused(project);

    /// <summary>
    /// ADR-015: risk management is required for the Standard and Full profiles; Light carries issues only. Read from the
    /// GOVERNANCE_PROFILE version in force, failing closed when there is none (422 CONFIGURATION_MISSING).
    /// </summary>
    public async Task<AdministrationError?> ProfileRefusedAsync(ProjectFacts project, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ResolvedConfiguration profiles = await configuration.ResolveAsync(ConfigurationFamilyCodes.GovernanceProfile, asOf, cancellationToken).ConfigureAwait(false);
        return profiles.RequireGovernanceProfile(project.GovernanceProfileItemId).RiskManagementRequired ? null : AdministrationError.Rule(RiskErrorCodes.NotInProfile);
    }

    /// <summary>The category is a PUBLISHED RISK_CATEGORY item.</summary>
    public async Task<AdministrationError?> CategoryRefusedAsync(Guid categoryItemId, CancellationToken cancellationToken)
    {
        try
        {
            await masterData.RequirePublishedItemAsync(MasterDataCatalogueCodes.RiskCategory, categoryItemId, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (ConfigurationMissingException)
        {
            return AdministrationError.Rule(RiskErrorCodes.CategoryInvalid, new FieldIssue("riskCategoryItemId", FieldIssue.NotFound));
        }
    }

    /// <summary>
    /// An owner holds some role over the project's anchors now — an internal holder, or an external one of the delivering entity
    /// itself (ADR-013), as IdentityAccess decides it.
    /// </summary>
    public async Task<AdministrationError?> OwnerRefusedAsync(ProjectFacts project, Guid ownerUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        Guid[] roleIds = [.. (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).Select(r => r.Id)];
        RoleHolderScope scope = new(project.Id, project.DepartmentId, project.ExternalEntityId);
        return await holders.FindHolderAsync(ownerUserId, roleIds, scope, cancellationToken).ConfigureAwait(false) is null
            ? AdministrationError.Rule(RiskErrorCodes.OwnerNotEligible, new FieldIssue("ownerUserId", FieldIssue.NotAllowed))
            : null;
    }

    /// <summary>A risk is identified on or before today.</summary>
    public static AdministrationError? IdentifiedDateRefused(DateOnly identifiedDate, DateTimeOffset now) =>
        identifiedDate > DateOnly.FromDateTime(now.UtcDateTime)
            ? AdministrationError.Rule(RiskErrorCodes.IdentifiedDateInvalid, new FieldIssue("identifiedDate", FieldIssue.NotAllowed))
            : null;
}
