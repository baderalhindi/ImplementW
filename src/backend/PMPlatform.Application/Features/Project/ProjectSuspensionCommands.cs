using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// WF-01's side of edge 7 (TASK-062): the two lifecycle edges WF-09 takes. Each finds its edge in <see cref="ProjectLifecycle"/> on the
/// tracked row and stages the change and its audit event; the caller's save commits them.
/// </summary>
internal sealed class ProjectSuspensionCommands(IProjectRepository repository, IAuditTrail audit, TimeProvider timeProvider) : IProjectSuspensionCommands
{
    public Task<AdministrationError?> SuspendAsync(ProjectSuspensionCommand command, CancellationToken cancellationToken) =>
        StageAsync(command, ProjectLifecycleState.Suspended, cancellationToken);

    public Task<AdministrationError?> ResumeAsync(ProjectSuspensionCommand command, CancellationToken cancellationToken) =>
        StageAsync(command, ProjectLifecycleState.Active, cancellationToken);

    private async Task<AdministrationError?> StageAsync(ProjectSuspensionCommand command, ProjectLifecycleState to, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ProjectEntity? project = await repository.FindAsync(command.ProjectId, null, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return AdministrationError.NotFound;
        }

        ProjectLifecycleState from = project.LifecycleState;
        if (!ProjectLifecycle.Allows(from, to))
        {
            return from == ProjectLifecycleState.Closed ? AdministrationError.TerminalState : AdministrationError.InvalidTransition;
        }

        project.LifecycleState = to;
        project.UpdatedAt = timeProvider.GetUtcNow();
        project.UpdatedBy = command.ActorId;
        audit.Stage(ProjectAudit.SuspensionTransition(command, project, from));
        return null;
    }
}
