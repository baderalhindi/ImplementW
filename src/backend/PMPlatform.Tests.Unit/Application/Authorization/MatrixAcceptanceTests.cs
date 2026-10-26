using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.Authorization.AuthorizationScenario;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>
/// Build Pack 06 §A, items 1, 3, 4 and 5, against the shipped grants as each role's holder carries them (shipped-permission-matrix.md §5), and
/// the safety rules of <see cref="GrantPolicy"/> that no profile overrides (review C4). Items 2 (no self-approval) and 6 (no reports for an
/// external user) are decided past the engine: ApprovalAuthorityTests and ReportAcceptanceTests.
/// </summary>
public sealed class MatrixAcceptanceTests
{
    private static readonly string[] ApprovalAndReportPermissions =
        [PermissionCatalogue.ApprovalView, PermissionCatalogue.ApprovalDecide, PermissionCatalogue.ReportExport, PermissionCatalogue.ReportCompose];

    /// <summary>§A item 1: the System Administrator holds no approval and no business data, even when a profile grants them.</summary>
    [Fact]
    public async Task TheAdministratorHoldsNoApprovalAndNoBusinessDataWhateverAProfileGrants()
    {
        AuthorizationScenario administrator = new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(UserType.Internal,
        [
            .. Shipped("R01"),
            Grant("R01", PermissionCatalogue.ApprovalDecide, DataScope.All),
            Grant("R01", PermissionCatalogue.ProjectView, DataScope.All),
            Grant("R01", PermissionCatalogue.FinancialView, DataScope.All),
        ]);

        foreach (PermissionDefinition permission in PermissionCatalogue.Platform.Definitions.Where(p => GrantPolicy.BusinessGroups.Contains(p.Group)))
        {
            Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await administrator.AuthorizeAsync(permission.Code));
        }

        Assert.Equal(AuthorizationDecision.Allowed, await administrator.AuthorizeAsync(PermissionCatalogue.UserManage));
    }

    /// <summary>§A item 3: the Viewer reads its own department's records, nothing of another's, and writes nothing at all.</summary>
    [Fact]
    public async Task TheViewerIsReadOnlyAndLimitedToItsDepartment()
    {
        AuthorizationScenario viewer = new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(UserType.Internal, Shipped("R06", departmentId: DepartmentId));

        Assert.Equal(AuthorizationDecision.Allowed, await viewer.AuthorizeAsync(PermissionCatalogue.ProjectView, Project(DepartmentId)));
        Assert.Equal(AuthorizationOutcome.NotFound, (await viewer.AuthorizeAsync(PermissionCatalogue.ProjectView, Project(OtherDepartmentId))).Outcome);
        Assert.All(
            PermissionCatalogue.Platform.Definitions.Where(p => p.Mode == AccessMode.Write),
            permission => Assert.DoesNotContain(PermissionCatalogue.ShippedDefaultGrants, g => g.RoleCode == "R06" && g.PermissionCode == permission.Code));
        Assert.DoesNotContain(PermissionCatalogue.ShippedDefaultGrants, g => g.RoleCode == "R06" && g.PermissionCode == PermissionCatalogue.FinancialViewSensitive);
    }

    /// <summary>§A item 4: a Department Manager opens no other department's project — 404 by id, and absent from every list it reads.</summary>
    [Fact]
    public async Task ADepartmentManagerReachesItsOwnDepartmentOnly()
    {
        AuthorizationScenario manager = new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(UserType.Internal, Shipped("R03", departmentId: DepartmentId));

        foreach (string permission in new[] { PermissionCatalogue.ProjectView, PermissionCatalogue.FinancialView, PermissionCatalogue.RiskView, PermissionCatalogue.ApprovalDecide })
        {
            Assert.Equal(AuthorizationDecision.Allowed, await manager.AuthorizeAsync(permission, Project(DepartmentId)));
            Assert.Equal(AuthorizationOutcome.NotFound, (await manager.AuthorizeAsync(permission, Project(OtherDepartmentId))).Outcome);
            RecordScope scope = await manager.Engine.GetRecordScopeAsync(UserId, permission, CancellationToken.None);
            Assert.True(scope.Matches(Project(DepartmentId)));
            Assert.False(scope.Matches(Project(OtherDepartmentId)));
        }
    }

    /// <summary>
    /// §A item 5: an external user reaches its own entity's projects only, and never holds approval, reports or the SAR amounts — not even when
    /// a profile grants them, nor through R04 as an entity Project Manager.
    /// </summary>
    [Fact]
    public async Task AnExternalUserIsIsolatedToItsEntityAndHoldsNoApprovalReportOrAmount()
    {
        AuthorizationScenario entityUser = new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(UserType.External,
        [
            .. Shipped("R08", entityId: EntityId),
            .. Shipped("R04", entityId: EntityId, projectId: ProjectId),
        ]);

        Assert.Equal(AuthorizationDecision.Allowed, await entityUser.AuthorizeAsync(PermissionCatalogue.ProjectView, Project(DepartmentId, EntityId)));
        Assert.Equal(AuthorizationOutcome.NotFound, (await entityUser.AuthorizeAsync(PermissionCatalogue.ProjectView, Project(DepartmentId, OtherEntityId))).Outcome);
        Assert.Equal(AuthorizationOutcome.NotFound, (await entityUser.AuthorizeAsync(PermissionCatalogue.ProjectView, Project(DepartmentId))).Outcome);
        foreach (string permission in ApprovalAndReportPermissions.Append(PermissionCatalogue.FinancialViewSensitive).Append(PermissionCatalogue.IntegrationView))
        {
            Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await entityUser.AuthorizeAsync(permission));
        }
    }

    /// <summary>C4: R08 holds no approval, report, amount or integration permission, even held by an internal user.</summary>
    [Fact]
    public async Task TheExternalEntityUserRoleHoldsNoApprovalReportOrAmountEvenForAnInternalHolder()
    {
        AuthorizationScenario holder = new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(UserType.Internal,
            [.. ApprovalAndReportPermissions.Append(PermissionCatalogue.FinancialViewSensitive).Select(p => Grant("R08", p, DataScope.All))]);

        foreach (string permission in ApprovalAndReportPermissions.Append(PermissionCatalogue.FinancialViewSensitive))
        {
            Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await holder.AuthorizeAsync(permission));
        }
    }

    /// <summary>
    /// D-4: a Liaison assigned to a project reaches that project's records — the project, its risks, its external requests — and no other
    /// project's; a Liaison assigned to none reaches no project at all; and no Liaison sees financials (review C2).
    /// </summary>
    [Fact]
    public async Task ALiaisonReachesTheProjectsItIsAssignedToAndNoOther()
    {
        AuthorizationScenario assigned = new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(UserType.Internal, Shipped("R05", projectId: ProjectId));
        AuthorizationScenario unassigned = new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(UserType.Internal, Shipped("R05"));

        foreach (string permission in new[] { PermissionCatalogue.ProjectView, PermissionCatalogue.RiskView, PermissionCatalogue.ExternalRequestManage })
        {
            Assert.Equal(AuthorizationDecision.Allowed, await assigned.AuthorizeAsync(permission, Project(DepartmentId) with { ProjectId = ProjectId }));
            Assert.Equal(AuthorizationOutcome.NotFound, (await assigned.AuthorizeAsync(permission, Project(DepartmentId) with { ProjectId = OtherProjectId })).Outcome);
            Assert.Equal(AuthorizationOutcome.NotFound, (await unassigned.AuthorizeAsync(permission, Project(DepartmentId) with { ProjectId = ProjectId })).Outcome);
        }

        RecordScope scope = await assigned.Engine.GetRecordScopeAsync(UserId, PermissionCatalogue.ProjectView, CancellationToken.None);
        Assert.True(scope.Matches(Project(DepartmentId) with { ProjectId = ProjectId }));
        Assert.False(scope.Matches(Project(DepartmentId) with { ProjectId = OtherProjectId }));
        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await assigned.AuthorizeAsync(PermissionCatalogue.FinancialView));
        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await assigned.AuthorizeAsync(PermissionCatalogue.ApprovalView));
    }

    /// <summary>C1: the Portfolio Manager, the Department Manager and the Executive decide approvals; the Project Manager sees its own submissions.</summary>
    [Fact]
    public void ApprovalAuthorityShipsToTheManagersAndTheExecutive()
    {
        Assert.Equal(["R02", "R03", "R07"], RoleCodes.Where(role => PermissionCatalogue.ShippedDefaultGrants.Any(g => g.RoleCode == role && g.PermissionCode == PermissionCatalogue.ApprovalDecide)));
        Assert.Equal(["R02", "R03", "R04", "R07"], RoleCodes.Where(role => PermissionCatalogue.ShippedDefaultGrants.Any(g => g.RoleCode == role && g.PermissionCode == PermissionCatalogue.ApprovalView)));
    }

    /// <summary>D-4: whoever holds the SAR amounts holds them at the same scope as the financial view they qualify.</summary>
    [Fact]
    public void TheSensitiveAmountsGoWithTheFinancialViewAtItsScope() =>
        Assert.All(PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode == PermissionCatalogue.FinancialViewSensitive), sensitive =>
            Assert.Contains(PermissionCatalogue.ShippedDefaultGrants, g => g.RoleCode == sensitive.RoleCode && g.PermissionCode == PermissionCatalogue.FinancialView && g.Scope == sensitive.Scope));

    private static AuthorizationSubject Project(Guid departmentId, Guid? entityId = null) =>
        new() { ProjectId = ProjectId, DepartmentId = departmentId, ExternalEntityId = entityId, OwnerUserId = OtherUserId };

    private static EffectiveGrant[] Shipped(string role, Guid? departmentId = null, Guid? entityId = null, Guid? projectId = null) =>
        [.. PermissionCatalogue.ShippedDefaultGrants.Where(g => g.RoleCode == role).Select(g => Grant(role, g.PermissionCode, g.Scope, departmentId, entityId, projectId))];
}
