using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// WF-01's side of edge 8 (TASK-063): the three lifecycle edges WF-10 takes. Each finds its edge in <see cref="ProjectLifecycle"/> on the
/// tracked row and stages the change and its audit event; the caller's save commits them.
/// </summary>
internal sealed class ProjectCloseoutCommands(IProjectRepository repository, IAuditTrail audit, TimeProvider timeProvider) : IProjectCloseoutCommands
{
    public Task<AdministrationError?> CompleteAsync(ProjectCloseoutCommand command, CancellationToken cancellationToken) =>
        StageAsync(command, ProjectLifecycleState.Completed, cancellationToken);

    public Task<AdministrationError?> CloseAsync(ProjectCloseoutCommand command, CancellationToken cancellationToken) =>
        StageAsync(command, ProjectLifecycleState.Closed, cancellationToken);

    private async Task<AdministrationError?> StageAsync(ProjectCloseoutCommand command, ProjectLifecycleState to, CancellationToken cancellationToken)
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

        DateTimeOffset now = timeProvider.GetUtcNow();
        project.LifecycleState = to;
        project.ClosedAt = to == ProjectLifecycleState.Closed ? now : project.ClosedAt;
        project.UpdatedAt = now;
        project.UpdatedBy = command.ActorId;
        audit.Stage(ProjectAudit.CloseoutTransition(command, project, from));
        return null;
    }
}
