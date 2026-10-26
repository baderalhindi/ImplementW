using Microsoft.Extensions.Logging.Abstractions;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Tests.Unit.Application.Auditing;
using PMPlatform.Tests.Unit.Application.Authorization;

namespace PMPlatform.Tests.Unit.Application.Approval;

/// <summary>
/// The real authorization engine over in-memory principals, the platform catalogue, the eight canonical roles and
/// in-memory delegations: what <see cref="ApprovalAuthority"/> decides for a user on a task.
/// </summary>
internal sealed class ApprovalAuthorityScenario
{
    public static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    public static readonly Guid DepartmentId = Guid.Parse("00000000-0000-4000-8000-00000000b001");
    public static readonly Guid OtherDepartmentId = Guid.Parse("00000000-0000-4000-8000-00000000b002");
    public static readonly Guid ProjectId = Guid.Parse("00000000-0000-4000-8000-00000000d001");
    public static readonly Guid RequesterId = Guid.Parse("00000000-0000-4000-8000-00000000a000");

    public FakeAuthorizationRepository Principals { get; } = new();

    public FakeApprovalRepository Approvals { get; } = new();

    public RecordingAuditTrail Audit { get; } = new();

    public static Guid RoleId(string code) => Guid.Parse($"00000000-0000-4000-8000-0000000000{code[1..]}");

    public static Guid UserId(int n) => Guid.Parse($"00000000-0000-4000-8000-00000000a{n:D3}");

    public ApprovalAuthorityScenario WithUser(int n, UserType userType, params EffectiveGrant[] grants)
    {
        Principals.Principals[UserId(n)] = new AuthorizationPrincipal(UserId(n), userType, IsActive: true, DepartmentId, null, grants);
        return this;
    }

    public ApprovalAuthorityScenario WithDelegation(int delegator, int @delegate, string? routingKey = null) =>
        WithDelegation(UserId(delegator), UserId(@delegate), routingKey);

    public ApprovalAuthorityScenario WithDelegation(Guid delegator, Guid @delegate, string? routingKey = null)
    {
        Approvals.Delegations.Add(new ApprovalDelegation
        {
            Id = Guid.NewGuid(),
            DelegatorUserId = delegator,
            DelegateUserId = @delegate,
            RoutingKey = routingKey,
            ValidFrom = Now.AddDays(-1),
            ValidTo = Now.AddDays(7),
            Status = ApprovalDelegationStatus.Active,
        });
        return this;
    }

    /// <summary>A fresh authority, as each request gets: nothing read by an earlier question is reused.</summary>
    public ApprovalAuthority Authority()
    {
        AuthorizationEngine engine = new(Principals, PermissionCatalogue.Platform, Audit, NullLogger<AuthorizationEngine>.Instance);
        return new ApprovalAuthority(engine, new Roles(), Approvals, new FixedClock(Now));
    }

    public static EffectiveGrant Decide(string roleCode, DataScope scope, Guid? departmentId = null, Guid? projectId = null) =>
        new(roleCode, PermissionCatalogue.ApprovalDecide, scope, departmentId, null, projectId, null);

    public static ApprovalInstance Run(Guid? departmentId = null, Guid? projectId = null, string routingKey = "CHANGE_REQUEST") => new()
    {
        Id = Guid.NewGuid(),
        SubjectModule = "ChangeRequest",
        SubjectType = "ChangeRequest",
        SubjectId = Guid.NewGuid(),
        SubjectRevisionNo = 1,
        RoutingKey = routingKey,
        ScopeDepartmentId = departmentId ?? DepartmentId,
        ScopeProjectId = projectId,
        RequestedByUserId = RequesterId,
        OutcomeIdempotencyKey = "key",
        Status = ApprovalInstanceStatus.Pending,
    };

    public static ApprovalTask TaskFor(ApprovalInstance run, string roleCode) => new()
    {
        Id = Guid.NewGuid(),
        ApprovalInstanceId = run.Id,
        SequenceNo = 1,
        AssignedRoleId = RoleId(roleCode),
        Status = ApprovalTaskStatus.Pending,
    };

    private sealed class Roles : IRoleDirectory
    {
        public Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RoleSummary>>([.. AuthorizationScenario.RoleCodes.Select(code =>
                new RoleSummary(RoleId(code), code, new BilingualLabel(code, code), true, code is "R04" or "R08"))]);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
