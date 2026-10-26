using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Suspension.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 45, Closure → Suspension (command; TASK-063): WF-10's terminal path closes a SUSPENDED project without resuming it
/// (WF-10 §9), so the project's open suspension ends as PROJECT_CLOSED rather than by a resumption. The suspension stays WF-09's: it ends
/// the period and records the event under its own audit; its history is kept (WF-10 §7 WF-09 row).
/// </summary>
public interface ISuspensionClosureCommands
{
    /// <summary>
    /// Stages the end of the project's open suspension, as PROJECT_CLOSED, in the caller's unit of work, so it commits with the project's
    /// closure or not at all. Null when staged; InvalidTransition, with nothing staged, when the project has no open suspension.
    /// </summary>
    public Task<AdministrationError?> EndForClosureAsync(SuspensionClosureCommand command, CancellationToken cancellationToken);
}

/// <summary>The project, who closes it — a person, or WF-10's service principal — and the closure case effected.</summary>
public sealed record SuspensionClosureCommand(Guid ProjectId, Guid ActorId, AuditActorType ActorType, Guid ClosureCaseId);
