using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.IdentityAccess;
using static PMPlatform.Tests.Unit.Application.Authorization.AuthorizationScenario;

namespace PMPlatform.Tests.Unit.Application.Authorization;

/// <summary>
/// The shipped-default grants against the platform catalogue: every role × every permission. A granted cell has an allow
/// and a deny case; a withheld cell is denied, 403 at the gate and 404 on a record. Only the source-backed rows are shipped (seed record F-1).
/// </summary>
public sealed class ShippedGrantTests
{
    public static TheoryData<string, string> GrantedCells()
    {
        TheoryData<string, string> data = [];
        foreach (ShippedGrant grant in PermissionCatalogue.ShippedDefaultGrants)
        {
            data.Add(grant.RoleCode, grant.PermissionCode);
        }

        return data;
    }

    public static TheoryData<string, string> WithheldCells()
    {
        TheoryData<string, string> data = [];
        foreach (string role in RoleCodes)
        {
            foreach (PermissionDefinition permission in PermissionCatalogue.Platform.Definitions.Where(p => ShippedGrant(role, p.Code) is null))
            {
                data.Add(role, permission.Code);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GrantedCells))]
    public async Task AGrantedCellAllowsItsHolder(string role, string permission)
    {
        AuthorizationScenario scenario = Scenario(role);
        DataScope scope = ShippedGrant(role, permission)!.Scope;

        // The entity Project Manager owns the project their assignment covers: the one they manage (ADR-013).
        AuthorizationSubject inScope = scope == DataScope.Entity ? new AuthorizationSubject { ProjectId = ProjectId, ExternalEntityId = EntityId, OwnerUserId = OtherUserId }
            : IsEntityProjectManager(role) ? new AuthorizationSubject { ProjectId = ProjectId, ExternalEntityId = EntityId, OwnerUserId = UserId }
            : new AuthorizationSubject { OwnerUserId = UserId };

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(permission));
        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(permission, inScope));
    }

    [Theory]
    [MemberData(nameof(GrantedCells))]
    public async Task AGrantedCellDeniesOutsideItsScope(string role, string permission)
    {
        AuthorizationScenario scenario = Scenario(role);
        DataScope scope = ShippedGrant(role, permission)!.Scope;
        AuthorizationDecision outOfScope = AuthorizationDecision.NotFound(AuthorizationDenial.OutOfScope);

        switch (scope)
        {
            // Another user's layout or report definition, or a project someone else manages on the entity Project Manager's
            // own assignment: not visible, so 404 (R-47).
            case DataScope.Own:
                Assert.Equal(outOfScope, await scenario.AuthorizeAsync(permission, IsEntityProjectManager(role)
                    ? new AuthorizationSubject { ProjectId = ProjectId, ExternalEntityId = EntityId, OwnerUserId = OtherUserId }
                    : new AuthorizationSubject { OwnerUserId = OtherUserId }));
                break;

            // ALL leaves nothing out of scope; the same holder, disabled, holds nothing.
            case DataScope.All:
                Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.InactivePrincipal), await Disabled(scenario).AuthorizeAsync(permission));
                break;

            // ADR-013's entity Project Manager: another entity's project, and another project of their own entity, are both
            // out of reach; and the same grant held by an internal R04, who has no entity, reaches nothing.
            case DataScope.Entity:
                Assert.Equal(outOfScope, await scenario.AuthorizeAsync(permission, new AuthorizationSubject { ProjectId = ProjectId, ExternalEntityId = OtherEntityId }));
                Assert.Equal(outOfScope, await scenario.AuthorizeAsync(permission, new AuthorizationSubject { ProjectId = OtherProjectId, ExternalEntityId = EntityId }));
                AuthorizationScenario internalHolder = new AuthorizationScenario(PermissionCatalogue.Platform)
                    .WithUser(UserType.Internal, Grant(role, permission, DataScope.Entity));
                Assert.Equal(outOfScope, await internalHolder.AuthorizeAsync(permission, new AuthorizationSubject { ProjectId = ProjectId, ExternalEntityId = EntityId }));
                break;

            // No shipped grant has another scope yet; the one that first does adds its deny case here.
            case DataScope.Dept or DataScope.Assigned or DataScope.ReadOnly:
            default:
                throw new InvalidOperationException($"No deny case for {scope}.");
        }
    }

    [Theory]
    [MemberData(nameof(WithheldCells))]
    public async Task AWithheldCellIsDenied(string role, string permission)
    {
        AuthorizationScenario scenario = Scenario(role);

        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await scenario.AuthorizeAsync(permission));
        Assert.Equal(AuthorizationOutcome.NotFound, (await scenario.AuthorizeAsync(permission, new AuthorizationSubject { OwnerUserId = UserId })).Outcome);
    }

    /// <summary>ADR-019, verbatim: granted to R02, R03 and R07 and withheld from R04, R05, R06 and R08.</summary>
    [Theory]
    [InlineData(PermissionCatalogue.LayoutPersonalize)]
    [InlineData(PermissionCatalogue.ReportCompose)]
    public void PersonalizeLayoutAndComposeReportGoToR02R03AndR07Only(string permission) =>
        Assert.Equal(["R02", "R03", "R07"], RoleCodes.Where(role => ShippedGrant(role, permission) is not null));

    /// <summary>
    /// ADR-013's amendment to TASK-037, verbatim: entity Project Managers may upload, view and see version history on their own
    /// project. R04 at ENTITY, and nothing else: managing documents waits for Appendix A (document-management.md F-1).
    /// </summary>
    [Fact]
    public void DocumentViewAndUploadGoToTheEntityProjectManagerOnly()
    {
        Assert.Equal(
            [("R04", PermissionCatalogue.DocumentView, DataScope.Entity), ("R04", PermissionCatalogue.DocumentUpload, DataScope.Entity)],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("DOCUMENT_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    /// <summary>
    /// ADR-013's amendment to TASK-041, verbatim: external entity users may create a project draft for their own entity, and
    /// entities see their own projects. R04 and R08 view and register at ENTITY; AHDA's review and activation gates ship to
    /// no one until Appendix A (project-registration.md F-1).
    /// </summary>
    [Fact]
    public void ProjectViewAndRegisterGoToEntityUsersOnly()
    {
        Assert.Equal(
            [
                ("R04", PermissionCatalogue.ProjectView, DataScope.Entity), ("R04", PermissionCatalogue.ProjectRegister, DataScope.Entity),
                ("R08", PermissionCatalogue.ProjectView, DataScope.Entity), ("R08", PermissionCatalogue.ProjectRegister, DataScope.Entity),
            ],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("PROJECT_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    /// <summary>
    /// ADR-013: an External Entity User (R08, an entity-wide assignment) registers a new project of their own entity, and no
    /// other entity's, nor one with no entity at all.
    /// </summary>
    [Fact]
    public async Task AnEntityUserRegistersANewProjectOfTheirOwnEntityOnly()
    {
        AuthorizationScenario scenario = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R08", PermissionCatalogue.ProjectRegister, DataScope.Entity, entityId: EntityId));

        Assert.Equal(AuthorizationDecision.Allowed, await scenario.AuthorizeAsync(PermissionCatalogue.ProjectRegister, new AuthorizationSubject { DepartmentId = DepartmentId, ExternalEntityId = EntityId }));
        Assert.False((await scenario.AuthorizeAsync(PermissionCatalogue.ProjectRegister, new AuthorizationSubject { DepartmentId = DepartmentId, ExternalEntityId = OtherEntityId })).IsAllowed);
        Assert.False((await scenario.AuthorizeAsync(PermissionCatalogue.ProjectRegister, new AuthorizationSubject { DepartmentId = DepartmentId })).IsAllowed);
    }

    /// <summary>
    /// ADR-013's amendment to TASK-044, verbatim: an assigned entity Project Manager submits progress on their own project, and
    /// entities see progress and health on their own projects. R04 views and submits at OWN — the projects they manage — and
    /// R08 views at ENTITY; review ships to no one until Appendix A (progress-update.md F-2).
    /// </summary>
    [Fact]
    public void ProgressGoesToTheProjectManagerAndTheEntityOnly()
    {
        Assert.Equal(
            [
                ("R04", PermissionCatalogue.ProgressView, DataScope.Own), ("R04", PermissionCatalogue.ProgressSubmit, DataScope.Own),
                ("R08", PermissionCatalogue.ProgressView, DataScope.Entity),
            ],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("PROGRESS_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    /// <summary>
    /// ADR-013: an entity Project Manager — external, assigned to one project of their entity — submits progress on the
    /// project they manage, and on no other, not even another project of their own entity. An internal holder of R04 is
    /// held to the same rule: the scope is the project they manage, not their employer (R04 is employer-neutral).
    /// </summary>
    [Fact]
    public async Task OnlyTheProjectsOwnManagerSubmitsItsProgress()
    {
        AuthorizationScenario entityManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R04", PermissionCatalogue.ProgressSubmit, DataScope.Own, entityId: EntityId, projectId: ProjectId));
        AuthorizationScenario internalManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.Internal, Grant("R04", PermissionCatalogue.ProgressSubmit, DataScope.Own));

        Assert.Equal(AuthorizationDecision.Allowed, await entityManager.AuthorizeAsync(PermissionCatalogue.ProgressSubmit, Managed(ProjectId, EntityId, UserId)));
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.ProgressSubmit, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.ProgressSubmit, Managed(OtherProjectId, EntityId, UserId))).IsAllowed);
        Assert.Equal(AuthorizationDecision.Allowed, await internalManager.AuthorizeAsync(PermissionCatalogue.ProgressSubmit, Managed(OtherProjectId, null, UserId)));
        Assert.False((await internalManager.AuthorizeAsync(PermissionCatalogue.ProgressSubmit, Managed(OtherProjectId, null, OtherUserId))).IsAllowed);
    }

    /// <summary>The delivery team's decision of 2026-09-30 (notification-runtime.md F-1): WF-15 administration is R01's, at ALL, as FG-04's is.</summary>
    [Fact]
    public void NotificationTemplatesAndDeliveryOperationsGoToR01Only()
    {
        Assert.Equal(
            [
                ("R01", PermissionCatalogue.NotificationTemplateView, DataScope.All), ("R01", PermissionCatalogue.NotificationTemplateManage, DataScope.All),
                ("R01", PermissionCatalogue.NotificationDeliveryView, DataScope.All), ("R01", PermissionCatalogue.NotificationDeliveryManage, DataScope.All),
            ],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("NOTIFICATION_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    [Fact]
    public void OnlyR01ManagesIdentityIntegration() =>
        Assert.Equal(["R01"], RoleCodes.Where(role => ShippedGrant(role, PermissionCatalogue.IdentityIntegrationManage) is not null));

    [Fact]
    public void EveryShippedGrantNamesACataloguePermissionAndACanonicalRole() =>
        Assert.All(PermissionCatalogue.ShippedDefaultGrants, grant =>
        {
            Assert.True(PermissionCatalogue.Platform.Contains(grant.PermissionCode), grant.PermissionCode);
            Assert.Contains(grant.RoleCode, RoleCodes);
        });

    private static AuthorizationSubject Managed(Guid projectId, Guid? entityId, Guid projectManagerId) =>
        new() { ProjectId = projectId, DepartmentId = DepartmentId, ExternalEntityId = entityId, OwnerUserId = projectManagerId };

    private static ShippedGrant? ShippedGrant(string role, string permission) =>
        PermissionCatalogue.ShippedDefaultGrants.SingleOrDefault(g => g.RoleCode == role && g.PermissionCode == permission);

    private static AuthorizationScenario Disabled(AuthorizationScenario scenario)
    {
        scenario.Repository.Principals[UserId] = scenario.Repository.Principals[UserId] with { IsActive = false };
        return scenario;
    }

    /// <summary>
    /// The role's holder with every shipped grant of the role. A role with an ENTITY grant is held as ADR-013's entity
    /// Project Manager: an external user of <see cref="EntityId"/> whose assignment covers only <see cref="ProjectId"/>.
    /// </summary>
    private static bool IsEntityProjectManager(string role) =>
        PermissionCatalogue.ShippedDefaultGrants.Any(g => g.RoleCode == role && g.Scope == DataScope.Entity);

    private static AuthorizationScenario Scenario(string role)
    {
        List<ShippedGrant> grants = [.. PermissionCatalogue.ShippedDefaultGrants.Where(g => g.RoleCode == role)];
        bool entityProjectManager = IsEntityProjectManager(role);
        return new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(
            entityProjectManager ? UserType.External : UserTypeOf(role),
            [.. grants.Select(g => entityProjectManager
                ? Grant(role, g.PermissionCode, g.Scope, entityId: EntityId, projectId: ProjectId)
                : Grant(role, g.PermissionCode, g.Scope))]);
    }
}
