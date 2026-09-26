using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.Authorization.AuthorizationScenario;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>
/// Each term of the Section 10.1 formula beyond the data scope: project/business relationship and cross-entity isolation
/// (ADR-013), record/lifecycle state, workflow authority, sensitivity (ADR-010), the 403/404 split (R-47), and how grants
/// from several assignments combine.
/// </summary>
public sealed class EffectiveAuthorizationTests
{
    private static readonly Guid Internal = Guid.Parse("00000000-0000-4000-8000-000000000e01");
    private static readonly Guid Confidential = Guid.Parse("00000000-0000-4000-8000-000000000e02");
    private static readonly Guid Secret = Guid.Parse("00000000-0000-4000-8000-000000000e03");

    /// <summary>ADR-013: an entity Project Manager holds R04 scoped to their own project only.</summary>
    [Fact]
    public async Task AnEntityProjectManagerReachesOnlyTheirOwnProject()
    {
        AuthorizationScenario scenario = new AuthorizationScenario()
            .WithUser(UserType.External, Grant("R04", Write, DataScope.Assigned, entityId: EntityId, projectId: ProjectId));
        AuthorizationSubject ownProject = new() { ProjectId = ProjectId, ExternalEntityId = EntityId, AssignedUserIds = [UserId] };

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(Write, ownProject));
        // Another project of the same entity, even one they are assigned to.
        Assert.Equal(AuthorizationDecision.NotFound(AuthorizationDenial.OutOfScope), await scenario.AuthorizeAsync(Write, ownProject with { ProjectId = OtherProjectId }));
    }

    /// <summary>ADR-013: cross-entity isolation is absolute. Not even an ALL grant takes an external user outside their entity.</summary>
    [Fact]
    public async Task NoScopeTakesAnExternalUserOutsideTheirEntity()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.External, Grant("R08", Read, DataScope.All));

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(Read, new AuthorizationSubject { ExternalEntityId = EntityId }));
        Assert.Equal(AuthorizationOutcome.NotFound, (await scenario.AuthorizeAsync(Read, new AuthorizationSubject { ExternalEntityId = OtherEntityId })).Outcome);
        Assert.Equal(AuthorizationOutcome.NotFound, (await scenario.AuthorizeAsync(Read, new AuthorizationSubject())).Outcome);
    }

    [Fact]
    public async Task AnInternalUserWithAllScopeReachesEveryEntity()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R02", Read, DataScope.All));

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(Read, new AuthorizationSubject { ExternalEntityId = OtherEntityId }));
        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(Read, new AuthorizationSubject()));
    }

    [Fact]
    public async Task ALockedRecordCanBeReadButNotChanged()
    {
        AuthorizationScenario scenario = new AuthorizationScenario()
            .WithUser(UserType.Internal, Grant("R04", Read, DataScope.All), Grant("R04", Write, DataScope.All));
        AuthorizationSubject locked = new() { StateAllowsChange = false };

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(Read, locked));
        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.StateLocked), await scenario.AuthorizeAsync(Write, locked));
    }

    [Fact]
    public async Task AWorkflowStepIsActedOnOnlyByItsActors()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R03", Write, DataScope.All));

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(Write, new AuthorizationSubject { WorkflowActorUserIds = [UserId] }));
        Assert.Equal(
            AuthorizationDecision.Forbidden(AuthorizationDenial.NotWorkflowActor),
            await scenario.AuthorizeAsync(Write, new AuthorizationSubject { WorkflowActorUserIds = [OtherUserId] }));
    }

    /// <summary>ADR-010: a record classified above the permission's clearance is invisible; an unknown level clears nothing.</summary>
    [Theory]
    [InlineData("confidential", "secret", AuthorizationOutcome.NotFound)]
    [InlineData("confidential", "confidential", AuthorizationOutcome.Allowed)]
    [InlineData("secret", "internal", AuthorizationOutcome.Allowed)]
    [InlineData(null, "internal", AuthorizationOutcome.NotFound)]
    [InlineData("secret", "unknown", AuthorizationOutcome.NotFound)]
    [InlineData(null, null, AuthorizationOutcome.Allowed)]
    public async Task ARecordIsVisibleOnlyUpToThePermissionsClearance(string? clearance, string? classification, AuthorizationOutcome expected)
    {
        AuthorizationScenario scenario = WithClassifications(new AuthorizationScenario())
            .WithUser(UserType.Internal, Grant("R02", Read, DataScope.All, clearanceItemId: Level(clearance)));

        AuthorizationDecision decision = await scenario.AuthorizeAsync(Read, new AuthorizationSubject { DataClassificationItemId = Level(classification) });

        Assert.Equal(expected, decision.Outcome);
    }

    /// <summary>R-47: 403 only if the caller may see the record; a record they cannot see answers 404 whatever else is true.</summary>
    [Fact]
    public async Task AMissingPermissionIs403OnAVisibleRecordAnd404OnAnInvisibleOne()
    {
        AuthorizationScenario reader = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R06", Read, DataScope.All));
        AuthorizationScenario otherGroup = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R06", OtherRead, DataScope.All));
        AuthorizationSubject record = new() { DepartmentId = DepartmentId };

        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await reader.AuthorizeAsync(Write, record));
        Assert.Equal(AuthorizationDecision.NotFound(AuthorizationDenial.NotGranted), await otherGroup.AuthorizeAsync(Write, record));
    }

    [Fact]
    public async Task GrantsOfSeveralAssignmentsCombine()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(
            UserType.Internal,
            Grant("R03", Read, DataScope.Dept, departmentId: OtherDepartmentId),
            Grant("R04", Read, DataScope.Assigned, projectId: ProjectId));

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(Read, new AuthorizationSubject { DepartmentId = OtherDepartmentId }));
        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(Read, new AuthorizationSubject { ProjectId = ProjectId, AssignedUserIds = [UserId] }));
        Assert.Equal(AuthorizationOutcome.NotFound, (await scenario.AuthorizeAsync(Read, new AuthorizationSubject { DepartmentId = DepartmentId })).Outcome);
    }

    /// <summary>A DEPT assignment without its own anchor covers the user's directory department (ADR-007).</summary>
    [Fact]
    public async Task DeptScopeFallsBackToTheUsersDepartment()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R03", Read, DataScope.Dept));

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(Read, new AuthorizationSubject { DepartmentId = DepartmentId }));
        Assert.Equal(AuthorizationOutcome.NotFound, (await scenario.AuthorizeAsync(Read, new AuthorizationSubject { DepartmentId = OtherDepartmentId })).Outcome);
    }

    [Fact]
    public async Task ADisabledOrUnknownUserHoldsNothing()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R01", Read, DataScope.All));
        scenario.Repository.Principals[UserId] = scenario.Repository.Principals[UserId] with { IsActive = false };
        AuthorizationScenario unknown = new();

        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.InactivePrincipal), await scenario.AuthorizeAsync(Read));
        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.InactivePrincipal), await unknown.AuthorizeAsync(Read));
    }

    [Fact]
    public async Task APermissionOutsideTheCatalogueIsAProgrammingError()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R01", Read, DataScope.All));

        await Assert.ThrowsAsync<ArgumentException>(() => scenario.AuthorizeAsync("NOT_A_PERMISSION"));
    }

    [Fact]
    public async Task ThePrincipalIsReadOncePerRequest()
    {
        AuthorizationScenario scenario = new AuthorizationScenario().WithUser(UserType.Internal, Grant("R02", Read, DataScope.All));

        await scenario.AuthorizeAsync(Read);
        await scenario.AuthorizeAsync(Write);
        await scenario.Engine.GetPrincipalAsync(UserId, CancellationToken.None);

        Assert.Equal(1, scenario.Repository.PrincipalReads);
    }

    private static AuthorizationScenario WithClassifications(AuthorizationScenario scenario)
    {
        scenario.Repository.ClassificationRanks[Internal] = 1;
        scenario.Repository.ClassificationRanks[Confidential] = 2;
        scenario.Repository.ClassificationRanks[Secret] = 3;
        return scenario;
    }

    private static Guid? Level(string? name) => name switch
    {
        null => null,
        "internal" => Internal,
        "confidential" => Confidential,
        "secret" => Secret,
        _ => Guid.Parse("00000000-0000-4000-8000-000000000eff"),
    };
}
