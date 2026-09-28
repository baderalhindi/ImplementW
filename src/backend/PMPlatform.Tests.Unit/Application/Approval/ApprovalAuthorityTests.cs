using PMPlatform.Application.Features.Approval;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.Approval.ApprovalAuthorityScenario;

namespace PMPlatform.Tests.Unit.Application.Approval;

/// <summary>
/// TASK-035 authority evaluation via FG-03: the task's role, a covering scope, an active internal person who is not the
/// requester — and, under a delegation, the delegator's own authority at that moment and never more (acceptance
/// criterion 2; ADR-013).
/// </summary>
public sealed class ApprovalAuthorityTests
{
    [Fact]
    public async Task AHolderOfTheTaskRoleWithACoveringScopeDecidesByTheirOwnAuthority()
    {
        ApprovalAuthorityScenario scenario = new ApprovalAuthorityScenario().WithUser(1, UserType.Internal, Decide("R03", DataScope.Dept, DepartmentId));
        ApprovalInstance run = Run();

        AuthorityCheck check = await scenario.Authority().ResolveAsync(UserId(1), run, TaskFor(run, "R03"), CancellationToken.None);

        Assert.Equal(new ActingAuthority(UserId(1), null), check.Authority);
    }

    [Theory]
    [InlineData("R02", "R03", false)] // the permission, but through another role than the task's
    [InlineData("R03", "R03", true)] // the task's role, but for another department
    public async Task AGrantOfAnotherRoleOrScopeIsNoAuthority(string heldRole, string taskRole, bool otherDepartment)
    {
        ApprovalAuthorityScenario scenario = new ApprovalAuthorityScenario().WithUser(1, UserType.Internal, Decide(heldRole, DataScope.Dept, DepartmentId));
        ApprovalInstance run = Run(departmentId: otherDepartment ? OtherDepartmentId : DepartmentId);

        AuthorityCheck check = await scenario.Authority().ResolveAsync(UserId(1), run, TaskFor(run, taskRole), CancellationToken.None);

        Assert.Equal(ApprovalRefusal.NoAuthority, check.Refusal);
    }

    /// <summary>ADR-013: an external user holds no approval authority of any kind, whatever role and scope they hold.</summary>
    [Fact]
    public async Task AnExternalUserNeverDecidesEvenWithTheRoleAndScope()
    {
        ApprovalAuthorityScenario scenario = new ApprovalAuthorityScenario().WithUser(8, UserType.External, Decide("R04", DataScope.All));
        ApprovalInstance run = Run();

        AuthorityCheck check = await scenario.Authority().ResolveAsync(UserId(8), run, TaskFor(run, "R04"), CancellationToken.None);

        Assert.Equal(ApprovalRefusal.ExternalUser, check.Refusal);
    }

    [Fact]
    public async Task NoOneApprovesTheirOwnRequestNotEvenForADelegator()
    {
        ApprovalAuthorityScenario scenario = new ApprovalAuthorityScenario().WithUser(1, UserType.Internal, Decide("R03", DataScope.All));
        scenario.Principals.Principals[RequesterId] = new(RequesterId, UserType.Internal, true, DepartmentId, null, [Decide("R03", DataScope.All)]);
        scenario.WithDelegation(UserId(1), RequesterId);
        ApprovalInstance run = Run();

        AuthorityCheck check = await scenario.Authority().ResolveAsync(RequesterId, run, TaskFor(run, "R03"), CancellationToken.None);

        Assert.Equal(ApprovalRefusal.Requester, check.Refusal);
    }

    [Fact]
    public async Task ADelegateDecidesUnderTheDelegatorsAuthority()
    {
        ApprovalAuthorityScenario scenario = new ApprovalAuthorityScenario()
            .WithUser(1, UserType.Internal, Decide("R03", DataScope.Dept, DepartmentId))
            .WithUser(2, UserType.Internal)
            .WithDelegation(1, 2);
        ApprovalInstance run = Run();

        AuthorityCheck check = await scenario.Authority().ResolveAsync(UserId(2), run, TaskFor(run, "R03"), CancellationToken.None);

        Assert.Equal(UserId(1), check.Authority!.HolderUserId);
        Assert.Equal(scenario.Approvals.Delegations[0].Id, check.Authority.DelegationId);
    }

    /// <summary>Decision-time revalidation: the delegator's authority is read when the delegate acts, not when the delegation was given.</summary>
    [Fact]
    public async Task ADelegatorWhoLostTheirAuthorityConveysNone()
    {
        ApprovalAuthorityScenario scenario = new ApprovalAuthorityScenario()
            .WithUser(1, UserType.Internal, Decide("R03", DataScope.Dept, DepartmentId))
            .WithUser(2, UserType.Internal)
            .WithDelegation(1, 2);
        ApprovalInstance run = Run();
        Assert.NotNull((await scenario.Authority().ResolveAsync(UserId(2), run, TaskFor(run, "R03"), CancellationToken.None)).Authority);

        scenario.WithUser(1, UserType.Internal);

        Assert.Equal(ApprovalRefusal.NoAuthority, (await scenario.Authority().ResolveAsync(UserId(2), run, TaskFor(run, "R03"), CancellationToken.None)).Refusal);
    }

    [Fact]
    public async Task ADelegationForAnotherRoutingKeyConveysNothingHere()
    {
        ApprovalAuthorityScenario scenario = new ApprovalAuthorityScenario()
            .WithUser(1, UserType.Internal, Decide("R03", DataScope.All))
            .WithUser(2, UserType.Internal)
            .WithDelegation(1, 2, routingKey: "BASELINE");
        ApprovalInstance run = Run(routingKey: "CHANGE_REQUEST");

        Assert.Equal(ApprovalRefusal.NoAuthority, (await scenario.Authority().ResolveAsync(UserId(2), run, TaskFor(run, "R03"), CancellationToken.None)).Refusal);
    }

    /// <summary>ADR-013 across a delegation: no external person on either side.</summary>
    [Fact]
    public async Task NeitherAnExternalDelegateNorAnExternalDelegatorCarriesAuthority()
    {
        ApprovalAuthorityScenario scenario = new ApprovalAuthorityScenario()
            .WithUser(1, UserType.Internal, Decide("R04", DataScope.All))
            .WithUser(8, UserType.External)
            .WithUser(7, UserType.External, Decide("R04", DataScope.All))
            .WithUser(2, UserType.Internal)
            .WithDelegation(1, 8)
            .WithDelegation(7, 2);
        ApprovalInstance run = Run();

        Assert.Equal(ApprovalRefusal.ExternalUser, (await scenario.Authority().ResolveAsync(UserId(8), run, TaskFor(run, "R04"), CancellationToken.None)).Refusal);
        Assert.Equal(ApprovalRefusal.NoAuthority, (await scenario.Authority().ResolveAsync(UserId(2), run, TaskFor(run, "R04"), CancellationToken.None)).Refusal);
    }

    /// <summary>
    /// The workbook's validation check, as a property: along a chain 1 → 2 → 3 → 4, over every role and three scopes of
    /// run, a delegate may decide a task only where their delegator could by their own authority. Authority does not
    /// travel beyond the first link: 2 holds R02 of their own, so 3 gains R02 through 2 but never 1's R03, and 4 gains
    /// nothing, because 3 has no authority of their own.
    /// </summary>
    [Fact]
    public async Task AlongADelegationChainNoDelegateEverExceedsTheirDelegator()
    {
        ApprovalAuthorityScenario scenario = new ApprovalAuthorityScenario()
            .WithUser(1, UserType.Internal, Decide("R03", DataScope.Dept, DepartmentId), Decide("R07", DataScope.All))
            .WithUser(2, UserType.Internal, Decide("R02", DataScope.All))
            .WithUser(3, UserType.Internal)
            .WithUser(4, UserType.Internal)
            .WithDelegation(1, 2)
            .WithDelegation(2, 3)
            .WithDelegation(3, 4);
        ApprovalInstance[] runs = [Run(), Run(departmentId: OtherDepartmentId), Run(projectId: ProjectId)];
        string[] roles = ["R01", "R02", "R03", "R04", "R05", "R06", "R07", "R08"];

        List<string> decidable = [];
        foreach (ApprovalInstance run in runs)
        {
            foreach (string role in roles)
            {
                ApprovalTask task = TaskFor(run, role);
                for (int user = 1; user <= 4; user++)
                {
                    ApprovalAuthority authority = scenario.Authority();
                    if ((await authority.ResolveAsync(UserId(user), run, task, CancellationToken.None)).Authority is { } acting)
                    {
                        decidable.Add($"{user}:{role}:{Array.IndexOf(runs, run)}");
                        if (acting.DelegationId is not null)
                        {
                            ApprovalDelegation delegation = scenario.Approvals.Delegations.Single(d => d.Id == acting.DelegationId);
                            Assert.Equal(UserId(user), delegation.DelegateUserId);
                            Assert.Equal(delegation.DelegatorUserId, acting.HolderUserId);
                            Assert.Equal(
                                new ActingAuthority(acting.HolderUserId, null),
                                (await scenario.Authority().ResolveAsync(acting.HolderUserId, run, task, CancellationToken.None)).Authority);
                        }
                    }
                }
            }
        }

        Assert.Equal(
            [
                "2:R02:0", "3:R02:0", "1:R03:0", "2:R03:0", "1:R07:0", "2:R07:0",
                "2:R02:1", "3:R02:1", "1:R07:1", "2:R07:1",
                "2:R02:2", "3:R02:2", "1:R03:2", "2:R03:2", "1:R07:2", "2:R07:2",
            ],
            decidable);
    }
}
