using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.Authorization.AuthorizationScenario;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>
/// TASK-030's test criterion over every role and every scope: for each of R01–R08 holding a grant at each of ALL, DEPT,
/// OWN, ASSIGNED, ENTITY and READ-ONLY, one request the grant allows and one it denies. Appendix A decides which of
/// these 48 combinations ships (seed record F-1); the engine must evaluate each one correctly whichever it is. R08 is
/// an external user, so its records also lie in its own entity.
/// </summary>
public sealed class ScopeCoverageTests
{
    public static TheoryData<string, DataScope> RoleScopes()
    {
        TheoryData<string, DataScope> data = [];
        foreach (string role in RoleCodes)
        {
            foreach (DataScope scope in Enum.GetValues<DataScope>())
            {
                data.Add(role, scope);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RoleScopes))]
    public async Task EachScopeAllowsARecordItCovers(string role, DataScope scope)
    {
        AuthorizationScenario scenario = WithScopedGrants(role, scope);

        AuthorizationDecision decision = await scenario.AuthorizeAsync(Read, Covered());

        Assert.Equal(AuthorizationDecision.Allowed, decision);
    }

    [Theory]
    [MemberData(nameof(RoleScopes))]
    public async Task EachScopeDeniesARequestItDoesNotCover(string role, DataScope scope)
    {
        AuthorizationScenario scenario = WithScopedGrants(role, scope);

        (string permission, AuthorizationSubject subject, AuthorizationDecision expected) = scope switch
        {
            // ALL covers every record, so what it denies is a permission it does not hold, on a record it can see.
            DataScope.All => (Approve, Covered(), AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted)),
            DataScope.ReadOnly => (Write, Covered(), AuthorizationDecision.Forbidden(AuthorizationDenial.ReadOnlyScope)),
            // Outside the scope the record is invisible: 404, as for an id that does not exist (R-47).
            DataScope.Dept or DataScope.Own or DataScope.Assigned or DataScope.Entity =>
                (Read, NotCovered(scope), AuthorizationDecision.NotFound(AuthorizationDenial.OutOfScope)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };

        AuthorizationDecision decision = await scenario.AuthorizeAsync(permission, subject);

        Assert.Equal(expected, decision);
    }

    private static AuthorizationScenario WithScopedGrants(string role, DataScope scope) =>
        new AuthorizationScenario().WithUser(
            UserTypeOf(role),
            Grant(role, Read, scope, DepartmentId, EntityId),
            Grant(role, Write, scope, DepartmentId, EntityId));

    /// <summary>A record in the grant's department and entity, owned by and assigned to the user.</summary>
    private static AuthorizationSubject Covered() => new()
    {
        DepartmentId = DepartmentId,
        ExternalEntityId = EntityId,
        OwnerUserId = UserId,
        AssignedUserIds = [UserId],
    };

    /// <summary>The covered record with the one fact <paramref name="scope"/> tests moved outside it.</summary>
    private static AuthorizationSubject NotCovered(DataScope scope)
    {
        AuthorizationSubject covered = Covered();
        return scope switch
        {
            DataScope.Dept => covered with { DepartmentId = OtherDepartmentId },
            DataScope.Own => covered with { OwnerUserId = OtherUserId },
            DataScope.Assigned => covered with { AssignedUserIds = [OtherUserId] },
            // For the external R08 another entity is also outside cross-entity isolation; for the rest it is outside the ENTITY anchor.
            DataScope.Entity => covered with { ExternalEntityId = OtherEntityId },
            DataScope.All or DataScope.ReadOnly => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Covers every record."),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };
    }
}
