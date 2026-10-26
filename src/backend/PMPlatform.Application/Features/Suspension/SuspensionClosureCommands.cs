using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// WF-09's side of edge 45 (TASK-063): a SUSPENDED project closed without resuming ends its open suspension as PROJECT_CLOSED. The period
/// and its history stay WF-09's; the caller's save commits the end with the project's closure.
/// </summary>
internal sealed class SuspensionClosureCommands(ISuspensionRepository repository, IProjectFactsReader projects, IAuditTrail audit, TimeProvider timeProvider)
    : ISuspensionClosureCommands
{
    public async Task<AdministrationError?> EndForClosureAsync(SuspensionClosureCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ActiveSuspension? open = await repository.FindOpenSuspensionAsync(command.ProjectId, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = await projects.FindAsync(command.ProjectId, cancellationToken).ConfigureAwait(false);
        if (open is null || project is null)
        {
            return AdministrationError.InvalidTransition;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        open.EndedAt = now;
        open.EndReason = SuspensionEndReason.ProjectClosed;
        SuspensionGate.Touch(open, command.ActorId, now);
        audit.Stage(SuspensionAudit.EndedByClosure(command, project, open));
        return null;
    }
}
