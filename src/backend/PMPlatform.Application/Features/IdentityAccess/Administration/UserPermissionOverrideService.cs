using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <inheritdoc cref="IUserPermissionOverrideService"/>
internal sealed partial class UserPermissionOverrideService(
    IUserPermissionOverrideRepository overrides,
    IUserAdministrationRepository users,
    IRoleAdministrationRepository roles,
    PermissionCatalogue catalogue,
    AdministrationAccess access,
    IAuditTrail audit,
    TimeProvider timeProvider,
    ILogger<UserPermissionOverrideService> logger) : IUserPermissionOverrideService
{
    private const string PermissionCodeField = "permissionCode";

    public async Task<AdministrationResult<UserPermissions>> ListAsync(Guid actorId, Guid userId, CancellationToken cancellationToken)
    {
        Versioned<UserDetail>? user = await users.FindDetailAsync(userId, cancellationToken).ConfigureAwait(false);
        return user is null ? AdministrationError.NotFound
            : await access.CheckRecordAsync(actorId, PermissionCatalogue.RoleView, AdministrationAccess.SubjectOf(user.Value), cancellationToken).ConfigureAwait(false) is { } denied ? denied
            : await ReadAsync(userId, user.Value.UserType, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<UserPermissions>> SetAsync(
        Guid actorId, Guid userId, string permissionCode, UserPermissionOverrideChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        Target target = await FindTargetAsync(actorId, userId, permissionCode, cancellationToken).ConfigureAwait(false);
        if (target.Refusal is { } refused)
        {
            return refused;
        }

        if (change.Effect == PermissionOverrideEffect.Grant
            && await GrantRefusalAsync(userId, target.UserType, permissionCode, cancellationToken).ConfigureAwait(false) is { } notAllowed)
        {
            return notAllowed;
        }

        DataScope? scope = change.Effect == PermissionOverrideEffect.Grant ? change.DataScope : null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        UserPermissionOverride? existing = target.Existing;
        audit.Stage(Entry(actorId, userId, target.UserExternalEntityId, IdentityAccessAuditEvents.UserPermissionOverrideSet,
        [
            AuditAttribute.Of(IdentityAccessAuditAttributes.UserId, userId),
            AuditAttribute.Of(PermissionCodeAttribute, permissionCode),
            AuditAttribute.Change(EffectAttribute, existing?.Effect, change.Effect),
            AuditAttribute.Change(DataScopeAttribute, existing?.DataScope, scope),
            AuditAttribute.Of(ReasonAttribute, change.Reason.Text),
        ]));
        if (existing is null)
        {
            existing = new UserPermissionOverride
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                PermissionId = target.PermissionId,
                Reason = change.Reason,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            overrides.Add(existing);
        }

        existing.Effect = change.Effect;
        existing.DataScope = scope;
        existing.Reason = change.Reason;
        existing.UpdatedAt = now;
        existing.UpdatedBy = actorId;
        return await SaveAsync(actorId, userId, target.UserType, "set", permissionCode, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<UserPermissions>> ClearAsync(
        Guid actorId, Guid userId, string permissionCode, NarrativeText reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);

        Target target = await FindTargetAsync(actorId, userId, permissionCode, cancellationToken).ConfigureAwait(false);
        if (target.Refusal is { } refused)
        {
            return refused;
        }

        if (target.Existing is not { } existing)
        {
            return await ReadAsync(userId, target.UserType, cancellationToken).ConfigureAwait(false);
        }

        audit.Stage(Entry(actorId, userId, target.UserExternalEntityId, IdentityAccessAuditEvents.UserPermissionOverrideCleared,
        [
            AuditAttribute.Of(IdentityAccessAuditAttributes.UserId, userId),
            AuditAttribute.Of(PermissionCodeAttribute, permissionCode),
            AuditAttribute.Change(EffectAttribute, existing.Effect, null),
            AuditAttribute.Change(DataScopeAttribute, existing.DataScope, null),
            AuditAttribute.Of(ReasonAttribute, reason.Text),
        ]));
        overrides.Remove(existing);
        return await SaveAsync(actorId, userId, target.UserType, "cleared", permissionCode, cancellationToken).ConfigureAwait(false);
    }

    private const string PermissionCodeAttribute = "permission_code";
    private const string EffectAttribute = "effect";
    private const string DataScopeAttribute = "data_scope";
    private const string ReasonAttribute = "reason";

    private async Task<Target> FindTargetAsync(Guid actorId, Guid userId, string permissionCode, CancellationToken cancellationToken)
    {
        Versioned<UserDetail>? user = await users.FindDetailAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return new Target(AdministrationError.NotFound);
        }

        UserDetail detail = user.Value;
        if (await access.CheckRecordAsync(actorId, PermissionCatalogue.RoleAssign, AdministrationAccess.SubjectOf(detail), cancellationToken).ConfigureAwait(false) is { } denied)
        {
            return new Target(denied);
        }

        if (userId == actorId)
        {
            return new Target(AdministrationError.Rule(IdentityAccessErrorCodes.SelfAdministration));
        }

        if (detail.UserType == UserType.Service)
        {
            return new Target(AdministrationError.Rule(IdentityAccessErrorCodes.ServicePrincipal));
        }

        PermissionSummary? permission = (await roles.ListPermissionsAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(p => string.Equals(p.Code, permissionCode, StringComparison.Ordinal));
        if (permission is null || !catalogue.Contains(permissionCode))
        {
            return new Target(AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, new FieldIssue(PermissionCodeField, FieldIssue.NotFound)));
        }

        UserPermissionOverride? existing = (await overrides.ListAsync(userId, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(o => o.PermissionId == permission.Id);
        return new Target(null, detail.UserType, detail.ExternalEntityId, permission.Id, existing);
    }

    /// <summary>A grant joins a role the user holds, so they need one that <see cref="GrantPolicy"/> lets hold it. Otherwise it could never take effect.</summary>
    private async Task<AdministrationError?> GrantRefusalAsync(Guid userId, UserType userType, string permissionCode, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> held = await overrides.ListRoleCodesAsync(userId, cancellationToken).ConfigureAwait(false);
        return held.Count == 0 ? AdministrationError.Rule(IdentityAccessErrorCodes.UserHasNoRole)
            : held.Any(role => GrantPolicy.Permits(role, userType, catalogue.Get(permissionCode))) ? null
            : AdministrationError.Rule(IdentityAccessErrorCodes.ReferenceInvalid, new FieldIssue(PermissionCodeField, FieldIssue.NotAllowed));
    }

    private async Task<AdministrationResult<UserPermissions>> SaveAsync(
        Guid actorId, Guid userId, UserType userType, string change, string permissionCode, CancellationToken cancellationToken)
    {
        if ((await overrides.SaveAsync(cancellationToken).ConfigureAwait(false)).Error is { } saveError)
        {
            return saveError;
        }

        LogOverrideChanged(logger, actorId, change, permissionCode, userId);
        return await ReadAsync(userId, userType, cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserPermissions> ReadAsync(Guid userId, UserType userType, CancellationToken cancellationToken)
    {
        ILookup<string, RoleGrantHeld> fromRoles = (await overrides.ListRolePermissionsAsync(userId, cancellationToken).ConfigureAwait(false))
            .Where(g => catalogue.Contains(g.PermissionCode) && GrantPolicy.Permits(g.RoleCode, userType, catalogue.Get(g.PermissionCode)))
            .ToLookup(g => g.PermissionCode, StringComparer.Ordinal);
        Dictionary<Guid, UserPermissionOverride> adjusted = (await overrides.ListAsync(userId, cancellationToken).ConfigureAwait(false)).ToDictionary(o => o.PermissionId);
        IReadOnlyList<PermissionSummary> permissions = await roles.ListPermissionsAsync(cancellationToken).ConfigureAwait(false);
        return new UserPermissions(userId, [.. permissions.Select(p =>
        {
            adjusted.TryGetValue(p.Id, out UserPermissionOverride? o);
            UserRoleGrant[] given = [.. fromRoles[p.Code].Select(g => new UserRoleGrant(g.RoleCode, g.DataScope))];
            bool held = o?.Effect switch
            {
                PermissionOverrideEffect.Remove => false,
                PermissionOverrideEffect.Grant => true,
                _ => given.Length > 0,
            };
            return new UserPermissionEntry(p.Code, p.Name, p.PermissionGroup, p.IsPrivileged, given, o?.Effect, o?.DataScope, o?.Reason, held);
        })]);
    }

    private static AuditEntry Entry(Guid actorId, Guid userId, Guid? externalEntityId, string eventType, AuditAttribute?[] attributes) =>
        new(AuditEventClass.PermissionChange, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject("IdentityAccess", nameof(User), userId),
            ScopeExternalEntityId = externalEntityId,
            Attributes = [.. attributes.OfType<AuditAttribute>()],
        };

    private sealed record Target(
        AdministrationError? Refusal, UserType UserType = default, Guid? UserExternalEntityId = null, Guid PermissionId = default, UserPermissionOverride? Existing = null);

    // Ids and codes only (CTL-27). The audit event of the change is staged with it (CTL-25).
    [LoggerMessage(Level = LogLevel.Information, Message = "User permissions: {ActorId} {Change} {PermissionCode} for user {UserId}.")]
    private static partial void LogOverrideChanged(ILogger logger, Guid actorId, string change, string permissionCode, Guid userId);
}
