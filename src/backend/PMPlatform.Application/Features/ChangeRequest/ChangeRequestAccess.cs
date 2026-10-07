using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// The explicit authorization check every change request operation passes (M-7), decided on the project's anchors — the project, its
/// owning department, its delivering entity, its Project Manager as owner. Refusals are audited by the engine.
/// </summary>
internal sealed class ChangeRequestAccess(IAuthorizationEngine engine, IAuditTrail audit)
{
    public const string ExternalUser = "EXTERNAL_USER";

    /// <summary>Null when allowed; otherwise NotFound (R-47) or Forbidden.</summary>
    public async Task<AdministrationError?> CheckAsync(Guid callerId, string permissionCode, ProjectFacts project, CancellationToken cancellationToken) =>
        (await engine.AuthorizeAsync(callerId, new AuthorizationRequest(permissionCode, SubjectOf(project)), cancellationToken).ConfigureAwait(false)).Outcome switch
        {
            AuthorizationOutcome.Allowed => null,
            AuthorizationOutcome.NotFound => AdministrationError.NotFound,
            AuthorizationOutcome.Forbidden => AdministrationError.Forbidden,
            var outcome => throw new ArgumentOutOfRangeException(nameof(permissionCode), outcome, "Unknown authorization outcome."),
        };

    /// <summary>Whether the caller may see the project's change requests, for a collection: nothing is recorded, because an empty collection is not a refusal (R-3).</summary>
    public async Task<bool> CanViewAsync(Guid callerId, ProjectFacts project, CancellationToken cancellationToken) =>
        (await engine.EvaluateAsync(callerId, new AuthorizationRequest(PermissionCatalogue.ChangeRequestView, SubjectOf(project)), cancellationToken).ConfigureAwait(false)).IsAllowed;

    /// <summary>
    /// ADR-013's amendment to TASK-060: entity Project Managers may raise a change request; materiality, approval and implementation
    /// remain AHDA's. An external user is refused review and implementation whatever they hold — the check is on the person, since R04
    /// is held by internal and external users alike — and the refusal is audited here.
    /// </summary>
    public async Task<AdministrationError?> CheckInternalAsync(
        Guid callerId, string permissionCode, ProjectFacts project, Guid changeRequestId, CancellationToken cancellationToken)
    {
        if (await CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false) is { UserType: UserType.Internal })
        {
            return null;
        }

        await audit.RecordAsync(ChangeRequestAudit.AuthorityRefused(callerId, project, changeRequestId, permissionCode, ExternalUser)).ConfigureAwait(false);
        return AdministrationError.Forbidden;
    }

    private static AuthorizationSubject SubjectOf(ProjectFacts project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new AuthorizationSubject
        {
            ProjectId = project.Id,
            DepartmentId = project.DepartmentId,
            ExternalEntityId = project.ExternalEntityId,
            OwnerUserId = project.ProjectManagerUserId,
        };
    }
}
