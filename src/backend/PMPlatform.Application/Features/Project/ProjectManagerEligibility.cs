using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// Who may be a project's manager (ADR-013): R04 is employer-neutral, so the manager is not looked up in the AHDA directory
/// but among the people who hold R04 over the project's anchors now — an internal holder, or an external holder of the
/// delivering entity itself, while that entity is active (IdentityAccess's <see cref="IRoleHolderDirectory"/>).
/// </summary>
internal sealed class ProjectManagerEligibility(IRoleDirectory roles, IRoleHolderDirectory holders)
{
    public const string RoleCode = "R04";

    public async Task<bool> IsEligibleAsync(Guid userId, ProjectEntity project, CancellationToken cancellationToken)
    {
        RoleSummary projectManager = (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).Single(r => r.Code == RoleCode);
        RoleHolderScope scope = new(project.Id, project.DepartmentId, project.ExternalEntityId);
        return await holders.FindHolderAsync(userId, [projectManager.Id], scope, cancellationToken).ConfigureAwait(false) is not null;
    }
}
