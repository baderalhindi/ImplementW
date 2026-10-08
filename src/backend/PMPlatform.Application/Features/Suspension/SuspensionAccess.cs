using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// The explicit authorization check every suspension operation passes (M-7), decided on the project's anchors — the project, its owning
/// department, its delivering entity, its Project Manager as owner. Refusals are audited by the engine.
/// </summary>
internal sealed class SuspensionAccess(IAuthorizationEngine engine, IAuditTrail audit)
{
    public const string ExternalUser = "EXTERNAL_USER";

    /// <summary>
    /// Null when allowed; otherwise NotFound (R-47) or Forbidden, and for a write to a CLOSED project 409 PROJECT_CLOSED (WF-10, TASK-063):
    /// a closed project is read-only.
    /// </summary>
    public async Task<AdministrationError?> CheckAsync(Guid callerId, string permissionCode, ProjectFacts project, CancellationToken cancellationToken) =>
        (await engine.AuthorizeAsync(callerId, new AuthorizationRequest(permissionCode, SubjectOf(project)), cancellationToken).ConfigureAwait(false)).Outcome switch
        {
            AuthorizationOutcome.Allowed => ClosedProjectGuard.Refusal(project, permissionCode),
            AuthorizationOutcome.NotFound => AdministrationError.NotFound,
            AuthorizationOutcome.Forbidden => AdministrationError.Forbidden,
            var outcome => throw new ArgumentOutOfRangeException(nameof(permissionCode), outcome, "Unknown authorization outcome."),
        };

    /// <summary>Whether the caller may see the project's suspension records, for a collection: nothing is recorded, because an empty collection is not a refusal (R-3).</summary>
    public async Task<bool> CanViewAsync(Guid callerId, ProjectFacts project, CancellationToken cancellationToken) =>
        (await engine.EvaluateAsync(callerId, new AuthorizationRequest(PermissionCatalogue.SuspensionView, SubjectOf(project)), cancellationToken).ConfigureAwait(false)).IsAllowed;

    /// <summary>
    /// ADR-013: AHDA retains every approval and lifecycle gate. An external user is refused review and activation whatever they hold — the
    /// check is on the person, since R04 is held by internal and external users alike — and the refusal is audited here.
    /// </summary>
    public async Task<AdministrationError?> CheckInternalAsync(
        Guid callerId, string permissionCode, ProjectFacts project, Guid suspensionRequestId, CancellationToken cancellationToken)
    {
        if (await CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false) is { UserType: UserType.Internal })
        {
            return null;
        }

        await audit.RecordAsync(SuspensionAudit.AuthorityRefused(callerId, project, suspensionRequestId, permissionCode, ExternalUser)).ConfigureAwait(false);
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
