using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// The explicit authorization check every project operation passes (M-7): the engine decides on the project's anchors —
/// itself, its owning department, its delivering entity and its Project Manager as owner. Refusals are audited by the engine.
/// </summary>
internal sealed class ProjectAccess(IAuthorizationEngine engine, IAuditTrail audit)
{
    /// <summary>Null when allowed; otherwise NotFound (R-47) or Forbidden.</summary>
    public async Task<AdministrationError?> CheckAsync(Guid callerId, string permissionCode, ProjectEntity project, CancellationToken cancellationToken) =>
        Refusal(await engine.AuthorizeAsync(callerId, new AuthorizationRequest(permissionCode, SubjectOf(project)), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// For a project that does not exist yet, or anchors it is about to take: there is nothing to hide, so any refusal is
    /// Forbidden, whatever the engine's R-47 answer.
    /// </summary>
    public async Task<AdministrationError?> CheckAnchorsAsync(
        Guid callerId, string permissionCode, Guid? projectId, ProjectDraft draft, Guid? projectManagerUserId, CancellationToken cancellationToken)
    {
        AuthorizationSubject subject = new()
        {
            ProjectId = projectId,
            DepartmentId = draft.DepartmentId,
            ExternalEntityId = draft.ExternalEntityId,
            OwnerUserId = projectManagerUserId,
        };
        return (await engine.AuthorizeAsync(callerId, new AuthorizationRequest(permissionCode, subject), cancellationToken).ConfigureAwait(false)).IsAllowed
            ? null
            : AdministrationError.Forbidden;
    }

    /// <summary>
    /// An AHDA lifecycle gate — review, activation. ADR-013: AHDA retains every approval and lifecycle gate, so an external
    /// user is refused whatever role or grant they hold; the check is on the person, because R04 is held by internal and
    /// external users alike. That refusal is audited here.
    /// </summary>
    public async Task<AdministrationError?> CheckGateAsync(Guid callerId, string permissionCode, ProjectEntity project, CancellationToken cancellationToken)
    {
        if (await CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false) is { UserType: UserType.Internal })
        {
            return null;
        }

        await audit.RecordAsync(ProjectAudit.GateRefused(callerId, project, permissionCode)).ConfigureAwait(false);
        return AdministrationError.Forbidden;
    }

    public static AuthorizationSubject SubjectOf(ProjectEntity project) => new()
    {
        ProjectId = project.Id,
        DepartmentId = project.DepartmentId,
        ExternalEntityId = project.ExternalEntityId,
        OwnerUserId = project.ProjectManagerUserId,
    };

    private static AdministrationError? Refusal(AuthorizationDecision decision) => decision.Outcome switch
    {
        AuthorizationOutcome.Allowed => null,
        AuthorizationOutcome.NotFound => AdministrationError.NotFound,
        AuthorizationOutcome.Forbidden => AdministrationError.Forbidden,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision.Outcome, "Unknown authorization outcome."),
    };
}
