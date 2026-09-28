using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.Authorization.AuthorizationScenario;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>TASK-033, CTL-25: every refusal the engine decides is an AUTHORIZATION_DENIAL audit event; an allowed request is not audited.</summary>
public sealed class AuthorizationAuditTests
{
    [Fact]
    public async Task ARefusalIsAuditedWithItsReasonAndTheRecordsAnchors()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R03", Write, DataScope.Dept, departmentId: DepartmentId));
        AuthorizationSubject otherDepartment = new() { DepartmentId = Guid.NewGuid(), ProjectId = ProjectId };

        await scenario.AuthorizeAsync(Write, otherDepartment);

        AuditEntry denied = Assert.Single(scenario.Audit.Recorded);
        Assert.Equal((AuditEventClass.AuthorizationDenial, "IdentityAccess.AccessDenied", AuditOutcome.Denied), (denied.EventClass, denied.EventType, denied.Outcome));
        Assert.Equal((UserId, ProjectId), (denied.ActorUserId, denied.ScopeProjectId));
        Assert.Equal(
            ["decision_outcome=NOT_FOUND", "denial_reason=OUT_OF_SCOPE", $"department_id={otherDepartment.DepartmentId}", $"permission_code={Write}"],
            denied.Attributes.Select(a => $"{a.Name}={a.NewValue}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnAllowedRequestIsNotAudited()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R02", Read, DataScope.All));

        Assert.True((await scenario.AuthorizeAsync(Read, new AuthorizationSubject())).IsAllowed);
        Assert.Empty(scenario.Audit.All);
    }

    /// <summary>A token whose user does not exist cannot name an actor the store holds: the id is recorded as the subject.</summary>
    [Fact]
    public async Task AnUnknownUserIsTheSubjectNotTheActor()
    {
        AuthorizationScenario scenario = new();

        await scenario.AuthorizeAsync(Read, new AuthorizationSubject());

        AuditEntry denied = Assert.Single(scenario.Audit.Recorded);
        Assert.Null(denied.ActorUserId);
        Assert.Equal(new AuditSubject("IdentityAccess", "User", UserId), denied.Subject);
    }
}
