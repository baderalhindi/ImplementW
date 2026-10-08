using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// The explicit authorization check every FinancialKpi operation passes (M-7), decided on the project's anchors: the project, its
/// owning department, its delivering entity, and its Project Manager as owner. Refusals are audited by the engine. The masks of
/// ADR-010 are read here too, so every representation of a record applies the same one (CTL-19).
/// </summary>
internal sealed class FinancialKpiAccess(IAuthorizationEngine engine, IAuditTrail audit)
{
    public const string ExternalUser = "EXTERNAL_USER";
    public const string Submitter = "SUBMITTER";

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

    /// <summary>Whether the caller may read the project under <paramref name="permissionCode"/>, for a collection: nothing is recorded (R-3).</summary>
    public async Task<bool> CanAsync(Guid callerId, string permissionCode, ProjectFacts project, CancellationToken cancellationToken) =>
        (await engine.EvaluateAsync(callerId, new AuthorizationRequest(permissionCode, SubjectOf(project)), cancellationToken).ConfigureAwait(false)).IsAllowed;

    /// <summary>
    /// AHDA's gate over what is published (ADR-013: entities see, AHDA governs): an external user is refused whatever they hold,
    /// and so is the person whose figures are being published — no one publishes their own. Both refusals are audited here.
    /// </summary>
    public async Task<AdministrationError?> CheckGateAsync(
        Guid callerId, string permissionCode, ProjectFacts project, Guid? authorId, Func<string, AuditEntry> refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        if (await CheckAsync(callerId, permissionCode, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        string? reason = await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false) is not { UserType: UserType.Internal } ? ExternalUser
            : authorId == callerId ? Submitter
            : null;
        if (reason is null)
        {
            return null;
        }

        await audit.RecordAsync(refusal(reason)).ConfigureAwait(false);
        return AdministrationError.Forbidden;
    }

    /// <summary>What the caller's audience may not see of the entity's fields when reading it under <paramref name="permissionCode"/> (ADR-010).</summary>
    public Task<FieldMask> MaskAsync(Guid callerId, string permissionCode, string entityCode, CancellationToken cancellationToken) =>
        engine.GetFieldMaskAsync(callerId, permissionCode, entityCode, cancellationToken);

    /// <summary>Whether the user exists and is active: a KPI's owner must be.</summary>
    public async Task<bool> IsActiveUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await engine.GetPrincipalAsync(userId, cancellationToken).ConfigureAwait(false) is { IsActive: true };

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
