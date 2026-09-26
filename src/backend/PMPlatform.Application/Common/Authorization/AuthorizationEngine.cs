using Microsoft.Extensions.Logging;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// Evaluates the Section 10.1 formula over the grants of the profile versions the user's active assignments are bound
/// to (ADR-018). Grants combine by union: a request is allowed if any one grant allows it. Two rules bound every grant,
/// whatever its scope (ADR-013): a per-project assignment covers only its project, and an external user never reaches a
/// record of another entity or of no entity. Scoped: what it reads is read once per request.
/// </summary>
internal sealed partial class AuthorizationEngine(IAuthorizationRepository repository, PermissionCatalogue catalogue, ILogger<AuthorizationEngine> logger)
    : IAuthorizationEngine
{
    private readonly Dictionary<Guid, AuthorizationPrincipal?> _principals = [];
    private readonly Dictionary<string, IReadOnlyList<FieldClassification>> _fieldClassifications = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<Guid, int>? _classificationRanks;

    public async Task<AuthorizationPrincipal?> GetPrincipalAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!_principals.TryGetValue(userId, out AuthorizationPrincipal? principal))
        {
            principal = await repository.FindPrincipalAsync(userId, cancellationToken).ConfigureAwait(false);
            _principals[userId] = principal;
        }

        return principal;
    }

    public async Task<AuthorizationDecision> AuthorizeAsync(Guid userId, AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        PermissionDefinition permission = catalogue.Get(request.PermissionCode);
        AuthorizationPrincipal? principal = await GetPrincipalAsync(userId, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, int> ranks = request.Subject?.DataClassificationItemId is null
            ? new Dictionary<Guid, int>()
            : await GetClassificationRanksAsync(cancellationToken).ConfigureAwait(false);

        AuthorizationDecision decision = principal is not { IsActive: true }
            ? AuthorizationDecision.Forbidden(AuthorizationDenial.InactivePrincipal)
            : request.Subject is null
                ? DecideWithoutRecord(principal, permission)
                : DecideOnRecord(principal, permission, request.Subject, ranks);

        if (!decision.IsAllowed)
        {
            LogDenied(logger, userId, permission.Code, decision.Outcome, decision.Denial);
        }

        return decision;
    }

    public async Task<FieldMask> GetFieldMaskAsync(Guid userId, string permissionCode, string entityCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entityCode);
        catalogue.Get(permissionCode);

        if (!_fieldClassifications.TryGetValue(entityCode, out IReadOnlyList<FieldClassification>? fields))
        {
            fields = await repository.GetFieldClassificationsAsync(entityCode, cancellationToken).ConfigureAwait(false);
            _fieldClassifications[entityCode] = fields;
        }

        if (fields.Count == 0)
        {
            return FieldMask.None;
        }

        AuthorizationPrincipal? principal = await GetPrincipalAsync(userId, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, int> ranks = await GetClassificationRanksAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<EffectiveGrant> grants = principal is { IsActive: true }
            ? principal.Grants.Where(g => g.PermissionCode == permissionCode)
            : [];
        List<EffectiveGrant> audience = [.. grants];

        return new FieldMask(fields
            .Where(f => f.MaskingRule != MaskingRule.Reveal && !audience.Any(g => Clears(g, f.DataClassificationItemId, ranks)))
            .ToDictionary(f => f.FieldCode, f => f.MaskingRule, StringComparer.Ordinal));
    }

    private static AuthorizationDecision DecideWithoutRecord(AuthorizationPrincipal principal, PermissionDefinition permission)
    {
        List<EffectiveGrant> granted = [.. principal.Grants.Where(g => g.PermissionCode == permission.Code)];
        return granted.Count == 0 ? AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted)
            : permission.Mode == AccessMode.Write && granted.All(g => g.Scope == DataScope.ReadOnly) ? AuthorizationDecision.Forbidden(AuthorizationDenial.ReadOnlyScope)
            : AuthorizationDecision.Allowed;
    }

    private AuthorizationDecision DecideOnRecord(
        AuthorizationPrincipal principal, PermissionDefinition permission, AuthorizationSubject subject, IReadOnlyDictionary<Guid, int> ranks)
    {
        List<EffectiveGrant> granted = [.. principal.Grants.Where(g => g.PermissionCode == permission.Code)];
        List<EffectiveGrant> covering = [.. granted.Where(g => Covers(principal, g, subject))];
        List<EffectiveGrant> cleared = [.. covering.Where(g => Clears(g, subject.DataClassificationItemId, ranks))];

        AuthorizationDenial denial =
            granted.Count == 0 ? AuthorizationDenial.NotGranted
            : covering.Count == 0 ? AuthorizationDenial.OutOfScope
            : cleared.Count == 0 ? AuthorizationDenial.ClassificationExceeded
            : permission.Mode == AccessMode.Write && cleared.All(g => g.Scope == DataScope.ReadOnly) ? AuthorizationDenial.ReadOnlyScope
            : AuthorizationDenial.None;

        if (denial != AuthorizationDenial.None)
        {
            // R-47: 403 only if some permission on this kind of record lets the caller see this one; otherwise 404.
            bool visible = principal.Grants.Any(g =>
                catalogue.Contains(g.PermissionCode)
                && catalogue.Get(g.PermissionCode).Group == permission.Group
                && Covers(principal, g, subject)
                && Clears(g, subject.DataClassificationItemId, ranks));
            return visible ? AuthorizationDecision.Forbidden(denial) : AuthorizationDecision.NotFound(denial);
        }

        // Record/lifecycle state, then workflow authority: both apply only to a record the caller is otherwise allowed.
        return permission.Mode == AccessMode.Write && !subject.StateAllowsChange ? AuthorizationDecision.Forbidden(AuthorizationDenial.StateLocked)
            : subject.WorkflowActorUserIds is { } actors && !actors.Contains(principal.UserId) ? AuthorizationDecision.Forbidden(AuthorizationDenial.NotWorkflowActor)
            : AuthorizationDecision.Allowed;
    }

    /// <summary>The project/business relationship (ADR-013), then the data scope with the assignment's anchors.</summary>
    private static bool Covers(AuthorizationPrincipal principal, EffectiveGrant grant, AuthorizationSubject subject)
    {
        if (grant.ProjectId is { } project && project != subject.ProjectId)
        {
            return false;
        }

        // Cross-entity isolation is absolute (ADR-013): no scope takes an external user outside their own entity.
        bool isolated = principal.UserType == UserType.External
                        && (subject.ExternalEntityId is null || subject.ExternalEntityId != principal.ExternalEntityId);

        return !isolated && grant.Scope switch
        {
            DataScope.All or DataScope.ReadOnly => true,
            DataScope.Dept => subject.DepartmentId is { } department && department == (grant.DepartmentId ?? principal.DepartmentId),
            DataScope.Own => subject.OwnerUserId == principal.UserId,
            DataScope.Assigned => subject.AssignedUserIds.Contains(principal.UserId),
            DataScope.Entity => subject.ExternalEntityId is { } entity && entity == (grant.ExternalEntityId ?? principal.ExternalEntityId),
            _ => false,
        };
    }

    /// <summary>
    /// Unclassified data is cleared for everyone. Classified data needs a clearance of equal or higher rank; an unknown
    /// classification or clearance clears nothing (fail closed).
    /// </summary>
    private static bool Clears(EffectiveGrant grant, Guid? classificationItemId, IReadOnlyDictionary<Guid, int> ranks) =>
        classificationItemId is not { } classification
        || (ranks.TryGetValue(classification, out int required)
            && grant.ClearanceItemId is { } clearance
            && ranks.TryGetValue(clearance, out int held)
            && held >= required);

    private async Task<IReadOnlyDictionary<Guid, int>> GetClassificationRanksAsync(CancellationToken cancellationToken) =>
        _classificationRanks ??= await repository.GetClassificationRanksAsync(cancellationToken).ConfigureAwait(false);

    [LoggerMessage(Level = LogLevel.Information, Message = "Authorization denied: user {UserId}, permission {PermissionCode}: {Outcome} ({Denial}).")]
    private static partial void LogDenied(ILogger logger, Guid userId, string permissionCode, AuthorizationOutcome outcome, AuthorizationDenial denial);
}
