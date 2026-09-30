using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.Authorization.AuthorizationScenario;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>
/// <see cref="IAuthorizationEngine.GetRecordScopeAsync"/> (TASK-037): the clauses a collection is filtered by reach exactly
/// the records the engine allows one at a time. A list that disagreed with the single-record decision would either hide
/// what a user may read or show what they may not.
/// </summary>
public sealed class RecordScopeTests
{
    private static readonly Guid Low = Guid.Parse("00000000-0000-4000-8000-000000000e01");
    private static readonly Guid High = Guid.Parse("00000000-0000-4000-8000-000000000e02");
    private static readonly Guid Unranked = Guid.Parse("00000000-0000-4000-8000-000000000e03");

    private static readonly Guid?[] Entities = [EntityId, OtherEntityId, null];
    private static readonly Guid?[] Departments = [DepartmentId, OtherDepartmentId, null];
    private static readonly Guid?[] Projects = [ProjectId, OtherProjectId, null];
    private static readonly Guid?[] Owners = [UserId, OtherUserId, null];
    private static readonly Guid?[] Classifications = [null, null, Low, High, Unranked];
    private static readonly DataScope[] Scopes = Enum.GetValues<DataScope>();

    /// <summary>
    /// The workbook-style property: over 4,000 seeded random principals, grants and records, for a read and a write
    /// permission, the scope matches a record if and only if the engine allows it (lifecycle and workflow left open).
    /// </summary>
    [Fact]
    public async Task TheScopeMatchesExactlyTheRecordsTheEngineAllows()
    {
        Random random = new(37);
        int allowed = 0;
        for (int i = 0; i < 4000; i++)
        {
            AuthorizationScenario scenario = RandomScenario(random);
            AuthorizationSubject subject = RandomSubject(random);
            foreach (string permission in new[] { Read, Write })
            {
                RecordScope scope = await scenario.Engine.GetRecordScopeAsync(UserId, permission, CancellationToken.None);
                bool decided = (await scenario.Engine.EvaluateAsync(UserId, new AuthorizationRequest(permission, subject), CancellationToken.None)).IsAllowed;
                Assert.True(decided == scope.Matches(subject), $"case {i}, {permission}: engine {decided}, scope {!decided}");
                allowed += decided ? 1 : 0;
            }
        }

        // Both answers occur hundreds of times, so the property is exercised both ways (this seed: 695 allowed of 8,000).
        Assert.InRange(allowed, 500, 7500);
    }

    [Fact]
    public async Task AnExternalUserWithoutAnEntityReachesNothing()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.External, Grant("R08", Read, DataScope.All));
        scenario.Repository.Principals[UserId] = scenario.Repository.Principals[UserId] with { ExternalEntityId = null };

        Assert.True((await scenario.Engine.GetRecordScopeAsync(UserId, Read, CancellationToken.None)).IsEmpty);
    }

    [Fact]
    public async Task AWritePermissionsReadOnlyGrantReachesNothingAndAnInactiveUserNothingAtAll()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R06", Write, DataScope.ReadOnly), Grant("R06", Read, DataScope.All));

        Assert.True((await scenario.Engine.GetRecordScopeAsync(UserId, Write, CancellationToken.None)).IsEmpty);
        Assert.False((await scenario.Engine.GetRecordScopeAsync(UserId, Read, CancellationToken.None)).IsEmpty);

        // The engine reads the principal once per request, so the inactive holder is another request's.
        AuthorizationScenario inactive = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R06", Read, DataScope.All));
        inactive.Repository.Principals[UserId] = inactive.Repository.Principals[UserId] with { IsActive = false };
        Assert.True((await inactive.Engine.GetRecordScopeAsync(UserId, Read, CancellationToken.None)).IsEmpty);
    }

    /// <summary>ADR-013 as a filter: the entity Project Manager's clause names their project and their entity, and nothing else.</summary>
    [Fact]
    public async Task TheEntityProjectManagersClauseIsTheirProjectAndTheirEntity()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(
            UserType.External, Grant("R04", Read, DataScope.Entity, entityId: EntityId, projectId: ProjectId, clearanceItemId: Low));
        scenario.Repository.ClassificationRanks[Low] = 1;
        scenario.Repository.ClassificationRanks[High] = 2;

        RecordScopeClause clause = Assert.Single((await scenario.Engine.GetRecordScopeAsync(UserId, Read, CancellationToken.None)).Clauses);

        Assert.Equal<(Guid?, Guid?, Guid?, Guid?)>((ProjectId, EntityId, null, null), (clause.ProjectId, clause.ExternalEntityId, clause.DepartmentId, clause.OwnerUserId));
        Assert.Equal([Low], clause.ClearedClassificationIds);
    }

    private static AuthorizationScenario RandomScenario(Random random)
    {
        UserType type = random.Next(2) == 0 ? UserType.Internal : UserType.External;
        EffectiveGrant[] grants = [.. Enumerable.Range(0, random.Next(1, 6)).Select(_ => new EffectiveGrant(
            "R0" + random.Next(1, 9),
            Pick(random, [Read, Read, Write, Write, OtherRead]),
            Pick(random, Scopes),
            Pick(random, Departments),
            Pick(random, Entities),
            Pick(random, Projects),
            Pick(random, Classifications)))];
        AuthorizationScenario scenario = new();
        scenario.Repository.Principals[UserId] = new AuthorizationPrincipal(
            UserId, type, IsActive: random.Next(10) > 0, Pick(random, Departments), Pick(random, Entities), grants);
        scenario.Repository.ClassificationRanks[Low] = 1;
        scenario.Repository.ClassificationRanks[High] = 2;
        return scenario;
    }

    private static AuthorizationSubject RandomSubject(Random random) => new()
    {
        ProjectId = Pick(random, Projects),
        DepartmentId = Pick(random, Departments),
        ExternalEntityId = Pick(random, Entities),
        OwnerUserId = Pick(random, Owners),
        AssignedUserIds = random.Next(2) == 0 ? [UserId] : [OtherUserId],
        DataClassificationItemId = Pick(random, Classifications),
    };

    private static T Pick<T>(Random random, T[] values) => values[random.Next(values.Length)];
}
