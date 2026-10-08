using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// WF-10's Closed capability matrix (BR-CLO-020, CLO-CC-21; TASK-063): once CLOSED a project is terminal and read-only, so no record of it
/// takes a write. Every module asks this in the one access check all its operations pass, after the caller is authorized — so a write
/// permission on a closed project is refused 409 PROJECT_CLOSED, while reads go on and a caller who may not see the project still gets 404.
/// </summary>
public static class ClosedProjectGuard
{
    /// <summary>409 <c>PROJECT_CLOSED</c> when <paramref name="permissionCode"/> writes and the project is CLOSED; otherwise null.</summary>
    public static AdministrationError? Refusal(ProjectFacts project, string permissionCode)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Status == ProjectLifecycleState.Closed && PermissionCatalogue.Platform.Get(permissionCode).Mode == AccessMode.Write
            ? AdministrationError.Conflict(ProjectErrorCodes.Closed)
            : null;
    }
}
