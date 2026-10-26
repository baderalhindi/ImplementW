using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.Approval;

/// <summary>
/// Authority over an approval task, evaluated by FG-03 (E-U1) at the moment it is exercised. A user holds authority
/// over a task when they are an active internal user, did not request the run, and hold APPROVAL_DECIDE through the
/// task's role with a grant whose scope covers the run's anchors. Scoped: principals, roles and delegations are read
/// once per request.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-013 — no external approval authority of any kind.</b> An external user never decides, delegates, receives a
/// delegation or is the authority behind a decision, whatever role or grant they hold: the check is on the person
/// (<see cref="UserType.External"/>), because roles such as R04 are held by internal and external users alike, so no
/// role and no authority matrix row can express it (TASK-034 record F-13). The database refuses an external user as an
/// approval task's decider or authority, or as either side of a delegation, as a second line (TASK-035 migration).
/// </para>
/// <para>
/// <b>No expansion of authority by delegation.</b> A delegate acts under a delegator's own authority, evaluated for that
/// delegator at decision time, within the delegation's routing key and period. What the delegator holds only through a
/// delegation is not their own authority, so it is not passed on: a chain of delegations conveys nothing beyond its
/// first link, and a delegate's effective authority is never more than a delegator's.
/// </para>
/// </remarks>
internal sealed class ApprovalAuthority(IAuthorizationEngine engine, IRoleDirectory roles, IApprovalRepository repository, TimeProvider timeProvider)
{
    private readonly Dictionary<Guid, IReadOnlyList<ApprovalDelegation>> _delegations = [];
    private IReadOnlyDictionary<Guid, string>? _roleCodes;

    /// <summary>
    /// Whether <paramref name="actorId"/> may decide <paramref name="task"/> now, and under whose authority: the actor's
    /// own first, then each delegation to them, oldest first.
    /// </summary>
    public async Task<AuthorityCheck> ResolveAsync(Guid actorId, ApprovalInstance instance, ApprovalTask task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(task);

        ApprovalRefusal refusal = await PersonRefusalAsync(actorId, instance, cancellationToken).ConfigureAwait(false);
        if (refusal != ApprovalRefusal.None)
        {
            return AuthorityCheck.Refused(refusal);
        }

        if (await HoldsAsync(actorId, instance, task.AssignedRoleId, cancellationToken).ConfigureAwait(false))
        {
            return new AuthorityCheck(new ActingAuthority(actorId, null), ApprovalRefusal.None);
        }

        foreach (ApprovalDelegation delegation in await DelegationsToAsync(actorId, cancellationToken).ConfigureAwait(false))
        {
            if (Covers(delegation, instance)
                && await PersonRefusalAsync(delegation.DelegatorUserId, instance, cancellationToken).ConfigureAwait(false) == ApprovalRefusal.None
                && await HoldsAsync(delegation.DelegatorUserId, instance, task.AssignedRoleId, cancellationToken).ConfigureAwait(false))
            {
                return new AuthorityCheck(new ActingAuthority(delegation.DelegatorUserId, delegation.Id), ApprovalRefusal.None);
            }
        }

        return AuthorityCheck.Refused(ApprovalRefusal.NoAuthority);
    }

    /// <summary>
    /// The roles an inbox looks in for <paramref name="userId"/>: those they hold APPROVAL_DECIDE through, and those
    /// each delegator of theirs does. A superset: every task found is still resolved one by one.
    /// </summary>
    public async Task<IReadOnlyCollection<Guid>> CandidateRoleIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        HashSet<string> codes = [.. await DecidingRoleCodesAsync(userId, cancellationToken).ConfigureAwait(false)];
        foreach (ApprovalDelegation delegation in await DelegationsToAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            codes.UnionWith(await DecidingRoleCodesAsync(delegation.DelegatorUserId, cancellationToken).ConfigureAwait(false));
        }

        IReadOnlyDictionary<Guid, string> roleCodes = await RoleCodesAsync(cancellationToken).ConfigureAwait(false);
        return [.. roleCodes.Where(role => codes.Contains(role.Value)).Select(role => role.Key)];
    }

    /// <summary>
    /// Whether the run is visible to <paramref name="userId"/> (R-47): the requester sees their own run; anyone else
    /// needs APPROVAL_VIEW over its anchors, or authority over one of its current tasks.
    /// </summary>
    public async Task<bool> CanViewAsync(Guid userId, ApprovalInstance instance, IReadOnlyList<ApprovalTask> tasks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(tasks);

        if (instance.RequestedByUserId == userId
            || (await engine.EvaluateAsync(userId, new AuthorizationRequest(PermissionCatalogue.ApprovalView, ViewSubject(instance)), cancellationToken).ConfigureAwait(false)).IsAllowed)
        {
            return true;
        }

        foreach (ApprovalTask task in tasks.Where(t => ApprovalStages.IsActionable(instance, tasks, t)))
        {
            if ((await ResolveAsync(userId, instance, task, cancellationToken).ConfigureAwait(false)).Authority is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="userId"/> may act on approvals at all (ADR-013): an active internal user.</summary>
    public async Task<bool> IsActiveInternalAsync(Guid userId, CancellationToken cancellationToken) =>
        await engine.GetPrincipalAsync(userId, cancellationToken).ConfigureAwait(false) is { IsActive: true, UserType: UserType.Internal };

    /// <summary>The run's anchors as the engine evaluates them (M-7); the requester is the owner, for OWN scope.</summary>
    public static AuthorizationSubject ViewSubject(ApprovalInstance instance) => new()
    {
        ProjectId = instance.ScopeProjectId,
        DepartmentId = instance.ScopeDepartmentId,
        OwnerUserId = instance.RequestedByUserId,
    };

    private async Task<ApprovalRefusal> PersonRefusalAsync(Guid userId, ApprovalInstance instance, CancellationToken cancellationToken) =>
        await engine.GetPrincipalAsync(userId, cancellationToken).ConfigureAwait(false) switch
        {
            null or { IsActive: false } => ApprovalRefusal.InactiveUser,
            { UserType: not UserType.Internal } => ApprovalRefusal.ExternalUser,
            _ when userId == instance.RequestedByUserId => ApprovalRefusal.Requester,
            _ => ApprovalRefusal.None,
        };

    /// <summary>The user's own authority over the role's task on this run: APPROVAL_DECIDE through that role, covering the anchors.</summary>
    private async Task<bool> HoldsAsync(Guid userId, ApprovalInstance instance, Guid roleId, CancellationToken cancellationToken)
    {
        if (!(await RoleCodesAsync(cancellationToken).ConfigureAwait(false)).TryGetValue(roleId, out string? roleCode))
        {
            return false;
        }

        AuthorizationRequest request = new(PermissionCatalogue.ApprovalDecide, new AuthorizationSubject
        {
            ProjectId = instance.ScopeProjectId,
            DepartmentId = instance.ScopeDepartmentId,
        })
        {
            RoleCode = roleCode,
        };
        return (await engine.EvaluateAsync(userId, request, cancellationToken).ConfigureAwait(false)).IsAllowed;
    }

    private async Task<IEnumerable<string>> DecidingRoleCodesAsync(Guid userId, CancellationToken cancellationToken) =>
        await engine.GetPrincipalAsync(userId, cancellationToken).ConfigureAwait(false) is { IsActive: true, UserType: UserType.Internal } principal
            ? principal.Grants.Where(g => g.PermissionCode == PermissionCatalogue.ApprovalDecide).Select(g => g.RoleCode)
            : [];

    private static bool Covers(ApprovalDelegation delegation, ApprovalInstance instance) =>
        delegation.RoutingKey is null || string.Equals(delegation.RoutingKey, instance.RoutingKey, StringComparison.Ordinal);

    private async Task<IReadOnlyList<ApprovalDelegation>> DelegationsToAsync(Guid delegateUserId, CancellationToken cancellationToken)
    {
        if (!_delegations.TryGetValue(delegateUserId, out IReadOnlyList<ApprovalDelegation>? delegations))
        {
            delegations = await repository.FindDelegationsToAsync(delegateUserId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            _delegations[delegateUserId] = delegations;
        }

        return delegations;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> RoleCodesAsync(CancellationToken cancellationToken) =>
        _roleCodes ??= (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Id, r => r.Code);
}
