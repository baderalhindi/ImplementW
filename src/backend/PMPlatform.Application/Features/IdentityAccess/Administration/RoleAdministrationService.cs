using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADM-006–009 (TASK-031). Roles, permissions and profiles are platform-wide reference data with no record scope, so the
/// endpoint gate (ROLE_VIEW, ROLE_MANAGE) is their whole authorization. The only write is a role's bilingual name, which
/// belongs to AHDA's administrators (seed: wording is inserted once and never reverted). A rename is saved with its
/// PRIVILEGED_ACTION audit event (TASK-033).
/// </summary>
internal sealed partial class RoleAdministrationService(
    IRoleAdministrationRepository roles,
    IPermissionProfileAuthoringService profiles,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<RoleAdministrationService> logger) : IRoleAdministrationService
{
    public Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken) => roles.ListRolesAsync(cancellationToken);

    public async Task<AdministrationResult<Versioned<RoleDetail>>> GetRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
        await roles.FindRoleAsync(roleId, cancellationToken).ConfigureAwait(false) is { } role ? role : AdministrationError.NotFound;

    public async Task<AdministrationResult<Versioned<RoleDetail>>> RenameRoleAsync(
        Guid actorId, Guid roleId, BilingualLabel name, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        Role? role = await roles.FindRoleForUpdateAsync(roleId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return AdministrationError.NotFound;
        }

        audit.Stage(new AuditEntry(AuditEventClass.PrivilegedAction, IdentityAccessAuditEvents.RoleRenamed, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject("IdentityAccess", nameof(Role), role.Id),
            Attributes =
            [
                AuditAttribute.Of(IdentityAccessAuditAttributes.RoleCode, role.Code),
                .. new[] { AuditAttribute.Change("name_ar", role.Name.Ar, name.Ar), AuditAttribute.Change("name_en", role.Name.En, name.En) }.OfType<AuditAttribute>(),
            ],
        });
        role.Name = name;
        role.UpdatedAt = timeProvider.GetUtcNow();
        role.UpdatedBy = actorId;
        if ((await roles.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogRenamed(logger, actorId, role.Code);
        return await roles.FindRoleAsync(roleId, cancellationToken).ConfigureAwait(false)
               ?? throw new InvalidOperationException($"Role {roleId} was saved and cannot be read back.");
    }

    /// <summary>
    /// A new role and its draft profile (the 5 Nov 2026 review §6.2), which then takes the usual prepare, verify and publish route: a role grants nothing until its profile
    /// is published.
    /// </summary>
    public async Task<AdministrationResult<Versioned<RoleDetail>>> CreateRoleAsync(Guid actorId, RoleCreateDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (await roles.IsRoleCodeInUseAsync(draft.Code, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Conflict(IdentityAccessErrorCodes.DuplicateKey, new FieldIssue("code", FieldIssue.Duplicate));
        }

        Guid? sourceVersion = null;
        if (draft.SourceRoleId is { } sourceRole)
        {
            sourceVersion = await roles.FindPublishedVersionOfRoleAsync(sourceRole, cancellationToken).ConfigureAwait(false);
            if (sourceVersion is null)
            {
                return AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, new FieldIssue("sourceRoleId", FieldIssue.NotFound));
            }
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Role role = new()
        {
            Id = Guid.CreateVersion7(now),
            Code = draft.Code,
            Name = draft.Name,
            IsSystem = false,
            IsExternalEligible = draft.IsExternalEligible,
            IsActive = true,
            AllowedScopes = [.. draft.AllowedScopes],
            LandingDashboardCode = draft.LandingDashboardCode,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        roles.AddRole(role);
        audit.Stage(RoleEntry(IdentityAccessAuditEvents.RoleCreated, actorId, role, [AuditAttribute.Of("source_role_id", draft.SourceRoleId)]));
        if ((await roles.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        AdministrationResult<Versioned<PermissionProfileVersionDetail>> profile = await profiles.CreateProfileAsync(
            actorId, new PermissionProfileCreateDraft($"{draft.Code}-DEFAULT", draft.Name, role.Id, sourceVersion), cancellationToken).ConfigureAwait(false);
        return profile.Error is { } profileError ? profileError
            : await roles.FindRoleAsync(role.Id, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException($"Role {role.Id} was saved and cannot be read back.");
    }

    /// <summary>A role's settings an administrator may change: its name is <see cref="RenameRoleAsync"/>'s; here its scopes, landing and activation.</summary>
    public async Task<AdministrationResult<Versioned<RoleDetail>>> UpdateSettingsAsync(
        Guid actorId, Guid roleId, IReadOnlyList<string> allowedScopes, string? landingDashboardCode, bool? showCreateInHeader, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(allowedScopes);
        Role? role = await roles.FindRoleForUpdateAsync(roleId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return AdministrationError.NotFound;
        }

        audit.Stage(RoleEntry(IdentityAccessAuditEvents.RoleSettingsChanged, actorId, role,
            [AuditAttribute.Of("allowed_scopes", string.Join(',', allowedScopes)), AuditAttribute.Of("landing_dashboard_code", landingDashboardCode), AuditAttribute.Of("show_create_in_header", (showCreateInHeader ?? role.ShowCreateInHeader) ? "true" : "false")]));
        role.AllowedScopes = [.. allowedScopes];
        role.LandingDashboardCode = landingDashboardCode;
        role.ShowCreateInHeader = showCreateInHeader ?? role.ShowCreateInHeader;
        return await SaveRoleAsync(actorId, role, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A role with people assigned can be deactivated, not deleted (§6.2): it is offered for no new assignment, and those made run until ended.</summary>
    public async Task<AdministrationResult<Versioned<RoleDetail>>> SetActiveAsync(Guid actorId, Guid roleId, bool active, uint expectedVersion, CancellationToken cancellationToken)
    {
        Role? role = await roles.FindRoleForUpdateAsync(roleId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return AdministrationError.NotFound;
        }

        if (role.IsSystem)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.RoleSystem);
        }

        audit.Stage(RoleEntry(IdentityAccessAuditEvents.RoleActivationChanged, actorId, role, [AuditAttribute.Change("is_active", role.IsActive, active)]));
        role.IsActive = active;
        return await SaveRoleAsync(actorId, role, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes a created role nobody ever held, with its profiles.</summary>
    public async Task<AdministrationError?> DeleteRoleAsync(Guid actorId, Guid roleId, uint expectedVersion, CancellationToken cancellationToken)
    {
        Role? role = await roles.FindRoleForUpdateAsync(roleId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return AdministrationError.NotFound;
        }

        if (role.IsSystem)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.RoleSystem);
        }

        if (await roles.RoleHasAssignmentsAsync(roleId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.RoleInUse);
        }

        audit.Stage(RoleEntry(IdentityAccessAuditEvents.RoleDeleted, actorId, role, []));
        await roles.RemoveProfilesOfRoleAsync(roleId, cancellationToken).ConfigureAwait(false);
        roles.RemoveRole(role);
        return (await roles.SaveAsync(cancellationToken).ConfigureAwait(false)).Error;
    }

    private async Task<AdministrationResult<Versioned<RoleDetail>>> SaveRoleAsync(Guid actorId, Role role, CancellationToken cancellationToken)
    {
        role.UpdatedAt = timeProvider.GetUtcNow();
        role.UpdatedBy = actorId;
        return (await roles.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError ? saveError
            : await roles.FindRoleAsync(role.Id, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException($"Role {role.Id} was saved and cannot be read back.");
    }

    private static AuditEntry RoleEntry(string eventType, Guid actorId, Role role, IEnumerable<AuditAttribute?> attributes) =>
        new(AuditEventClass.PrivilegedAction, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject("IdentityAccess", nameof(Role), role.Id),
            Attributes = [AuditAttribute.Of(IdentityAccessAuditAttributes.RoleCode, role.Code), .. attributes.OfType<AuditAttribute>()],
        };

    public Task<IReadOnlyList<PermissionSummary>> ListPermissionsAsync(CancellationToken cancellationToken) => roles.ListPermissionsAsync(cancellationToken);

    public Task<PermissionProfilePage> ListProfilesAsync(Guid? baseRoleId, PageRequest page, CancellationToken cancellationToken) =>
        roles.ListProfilesAsync(baseRoleId, page, cancellationToken);

    public async Task<AdministrationResult<PermissionProfileDetail>> GetProfileAsync(Guid profileId, CancellationToken cancellationToken) =>
        await roles.FindProfileAsync(profileId, cancellationToken).ConfigureAwait(false) is { } profile ? profile : AdministrationError.NotFound;

    [LoggerMessage(Level = LogLevel.Information, Message = "Role administration: {ActorId} renamed role {RoleCode}.")]
    private static partial void LogRenamed(ILogger logger, Guid actorId, string roleCode);
}
