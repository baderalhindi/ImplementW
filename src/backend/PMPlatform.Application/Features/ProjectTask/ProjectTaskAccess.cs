using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// The explicit authorization check every task operation passes (M-7), decided on the project's anchors — the project, its
/// owning department, its delivering entity, its Project Manager as owner — and, for one task, its owner as the assigned
/// person (ASSIGNED scope). Refusals are audited by the engine.
/// </summary>
internal sealed class ProjectTaskAccess(IAuthorizationEngine engine)
{
    /// <summary>
    /// Null when allowed; otherwise NotFound (R-47) or Forbidden, and for a write to a CLOSED project 409 PROJECT_CLOSED (WF-10, TASK-063):
    /// a closed project is read-only.
    /// </summary>
    public async Task<AdministrationError?> CheckAsync(Guid callerId, string permissionCode, ProjectFacts project, Guid? assigneeUserId, CancellationToken cancellationToken) =>
        (await engine.AuthorizeAsync(callerId, new AuthorizationRequest(permissionCode, SubjectOf(project, assigneeUserId)), cancellationToken).ConfigureAwait(false)).Outcome switch
        {
            AuthorizationOutcome.Allowed => ClosedProjectGuard.Refusal(project, permissionCode),
            AuthorizationOutcome.NotFound => AdministrationError.NotFound,
            AuthorizationOutcome.Forbidden => AdministrationError.Forbidden,
            var outcome => throw new ArgumentOutOfRangeException(nameof(permissionCode), outcome, "Unknown authorization outcome."),
        };

    /// <summary>
    /// Whether the caller may see the project's tasks with <paramref name="assigneeUserId"/> as their owner (null: every task),
    /// for a collection: nothing is recorded, because an empty collection is not a refusal (R-3).
    /// </summary>
    public async Task<bool> CanViewAsync(Guid callerId, ProjectFacts project, Guid? assigneeUserId, CancellationToken cancellationToken) =>
        (await engine.EvaluateAsync(callerId, new AuthorizationRequest(PermissionCatalogue.TaskView, SubjectOf(project, assigneeUserId)), cancellationToken).ConfigureAwait(false)).IsAllowed;

    private static AuthorizationSubject SubjectOf(ProjectFacts project, Guid? assigneeUserId)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new AuthorizationSubject
        {
            ProjectId = project.Id,
            DepartmentId = project.DepartmentId,
            ExternalEntityId = project.ExternalEntityId,
            OwnerUserId = project.ProjectManagerUserId,
            AssignedUserIds = assigneeUserId is { } assignee ? [assignee] : [],
        };
    }
}
