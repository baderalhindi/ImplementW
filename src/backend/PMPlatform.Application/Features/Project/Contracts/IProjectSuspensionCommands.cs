using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 7, Suspension → Project (command): WF-09 has a project move ACTIVE → SUSPENDED or SUSPENDED → ACTIVE as it effects
/// an approved suspension or resumption request. The lifecycle stays WF-01's: Project checks the edge on the row it reads and records the
/// transition under its own audit event. The authority is the approved request WF-09 names, not a permission of the caller, so this is
/// not on <see cref="IProjectService"/>. Neither command touches anything but the project's lifecycle state: no baseline, forecast or
/// date moves with it (WF-09 P4, P5).
/// </summary>
public interface IProjectSuspensionCommands
{
    /// <summary>
    /// Stages ACTIVE → SUSPENDED in the caller's unit of work, so it commits with the caller's records or not at all (M-11); the project's
    /// row version makes a concurrent change of it fail the caller's save. Null when staged; otherwise the refusal, with nothing staged.
    /// </summary>
    public Task<AdministrationError?> SuspendAsync(ProjectSuspensionCommand command, CancellationToken cancellationToken);

    /// <summary>Stages SUSPENDED → ACTIVE, as <see cref="SuspendAsync"/> does. <c>activated_at</c> keeps the project's first activation.</summary>
    public Task<AdministrationError?> ResumeAsync(ProjectSuspensionCommand command, CancellationToken cancellationToken);
}

/// <summary>The project, who effects the transition — a person, or WF-09's service principal on the effective date — and the request effected.</summary>
public sealed record ProjectSuspensionCommand(Guid ProjectId, Guid ActorId, AuditActorType ActorType, Guid SuspensionRequestId);
