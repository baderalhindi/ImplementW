using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 8, Closure → Project (command; TASK-063): WF-10 has a project move ACTIVE → COMPLETED as it effects an approved
/// completion case, and COMPLETED → CLOSED, or SUSPENDED → CLOSED on the terminal path, as it effects an approved closure case. The
/// lifecycle stays WF-01's: Project checks the edge on the row it reads and records the transition under its own audit event. The
/// authority is the approved case WF-10 names, not a permission of the caller, so this is not on <see cref="IProjectService"/>. Nothing but
/// the project's lifecycle state — and, on closure, when it closed — moves with it: no task, milestone, risk, change or baseline (BR-CLO-011
/// to BR-CLO-017).
/// </summary>
public interface IProjectCloseoutCommands
{
    /// <summary>
    /// Stages ACTIVE → COMPLETED in the caller's unit of work, so it commits with the caller's records or not at all; the project's row
    /// version makes a concurrent change of it fail the caller's save. Null when staged; otherwise the refusal, with nothing staged.
    /// </summary>
    public Task<AdministrationError?> CompleteAsync(ProjectCloseoutCommand command, CancellationToken cancellationToken);

    /// <summary>Stages COMPLETED → CLOSED, or SUSPENDED → CLOSED, with <c>closed_at</c>, as <see cref="CompleteAsync"/> does. CLOSED is terminal.</summary>
    public Task<AdministrationError?> CloseAsync(ProjectCloseoutCommand command, CancellationToken cancellationToken);
}

/// <summary>The project, who effects the transition — a person, or WF-10's service principal — and the completion or closure case effected.</summary>
public sealed record ProjectCloseoutCommand(Guid ProjectId, Guid ActorId, AuditActorType ActorType, Guid CaseId);
