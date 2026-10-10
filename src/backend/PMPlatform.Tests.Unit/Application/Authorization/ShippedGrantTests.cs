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
            : scope == DataScope.Assigned ? new AuthorizationSubject { ProjectId = ProjectId, ExternalEntityId = EntityId, OwnerUserId = OtherUserId, AssignedUserIds = [UserId] }
            : scope == DataScope.Dept ? new AuthorizationSubject { ProjectId = ProjectId, DepartmentId = DepartmentId, OwnerUserId = OtherUserId }
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

            // Another department's record is out of reach (TASK-057: R03's escalations of its own department only).
            case DataScope.Dept:
                Assert.Equal(outOfScope, await scenario.AuthorizeAsync(permission, new AuthorizationSubject { ProjectId = ProjectId, DepartmentId = OtherDepartmentId, OwnerUserId = OtherUserId }));
                break;

            // TASK-066: a record of the holder's own entity and project assigned to someone else is out of scope — 403 where another
            // grant lets them see it (R-47), else 404 — and one assigned to them but of another entity is out of reach (ADR-013).
            case DataScope.Assigned:
                Assert.Equal(AuthorizationDenial.OutOfScope, (await scenario.AuthorizeAsync(permission,
                    new AuthorizationSubject { ProjectId = ProjectId, ExternalEntityId = EntityId, OwnerUserId = OtherUserId, AssignedUserIds = [OtherUserId] })).Denial);
                Assert.Equal(outOfScope, await scenario.AuthorizeAsync(permission,
                    new AuthorizationSubject { ProjectId = ProjectId, ExternalEntityId = OtherEntityId, OwnerUserId = OtherUserId, AssignedUserIds = [UserId] }));
                break;

            // No shipped grant has another scope yet; the one that first does adds its deny case here.
            case DataScope.ReadOnly:
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

        // R-47: 404, unless another permission of the same group lets the role see the record (the subject is the holder's own and names no
        // project, so only an unbound OWN or wider grant reaches it): then 403 — R02, R03 and R07 see their own under REPORT_COMPOSE, so a
        // REPORT_EXPORT they lack on it is 403 (TASK-071). The entity Project Manager's grants are bound to its project and reach nothing here.
        string group = PermissionCatalogue.Platform.Get(permission).Group;
        bool visible = !IsEntityProjectManager(role) && PermissionCatalogue.ShippedDefaultGrants.Any(g =>
            g.RoleCode == role && PermissionCatalogue.Platform.Get(g.PermissionCode).Group == group && g.Scope is DataScope.All or DataScope.Own or DataScope.ReadOnly);
        Assert.Equal(
            visible ? AuthorizationOutcome.Forbidden : AuthorizationOutcome.NotFound,
            (await scenario.AuthorizeAsync(permission, new AuthorizationSubject { OwnerUserId = UserId })).Outcome);
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

    /// <summary>
    /// WF-03's specification §3: the Project Manager owns the schedule — builds it, keeps its forecast, submits its baselines.
    /// R04 views and edits at OWN, the projects they manage; baseline approval is WF-11's APPROVAL_DECIDE, and the other roles'
    /// schedule grants wait for Appendix A (schedule-baseline.md F-2).
    /// </summary>
    [Fact]
    public void ScheduleGoesToTheProjectManagerOnly()
    {
        Assert.Equal(
            [("R04", PermissionCatalogue.ScheduleView, DataScope.Own), ("R04", PermissionCatalogue.ScheduleEdit, DataScope.Own)],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("SCHEDULE_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    /// <summary>ADR-013: an entity Project Manager edits the schedule of the project they manage and of no other, as an internal one does.</summary>
    [Fact]
    public async Task OnlyTheProjectsOwnManagerEditsItsSchedule()
    {
        AuthorizationScenario entityManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R04", PermissionCatalogue.ScheduleEdit, DataScope.Own, entityId: EntityId, projectId: ProjectId));

        Assert.Equal(AuthorizationDecision.Allowed, await entityManager.AuthorizeAsync(PermissionCatalogue.ScheduleEdit, Managed(ProjectId, EntityId, UserId)));
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.ScheduleEdit, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.ScheduleEdit, Managed(OtherProjectId, EntityId, UserId))).IsAllowed);
    }

    /// <summary>
    /// TASK-048: the Project Manager plans the tasks, may edit a task's actual percentage (ADR-009) and holds the controlled
    /// reopen, at OWN — the projects they manage. TASK-066's gate decision limits an external contributor's direct source action to
    /// assigned tasks (WF-13 Path A): R08 views and updates the tasks it owns, at ASSIGNED, and neither plans nor reopens. The other
    /// owners' ASSIGNED grants wait for Appendix A (project-task.md F-2).
    /// </summary>
    [Fact]
    public void TasksGoToTheProjectManagerAndToEntityUsersOnTheTasksTheyOwn()
    {
        Assert.Equal(
            [
                ("R04", PermissionCatalogue.TaskView, DataScope.Own), ("R04", PermissionCatalogue.TaskUpdate, DataScope.Own),
                ("R04", PermissionCatalogue.TaskManage, DataScope.Own), ("R04", PermissionCatalogue.TaskReopen, DataScope.Own),
                ("R08", PermissionCatalogue.TaskView, DataScope.Assigned), ("R08", PermissionCatalogue.TaskUpdate, DataScope.Assigned),
            ],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("TASK_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    /// <summary>ADR-013's amendment to TASK-048: an assigned entity Project Manager acts directly on the tasks of their own project, and of no other.</summary>
    [Fact]
    public async Task OnlyTheProjectsOwnManagerActsOnItsTasks()
    {
        AuthorizationScenario entityManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R04", PermissionCatalogue.TaskUpdate, DataScope.Own, entityId: EntityId, projectId: ProjectId));

        Assert.Equal(AuthorizationDecision.Allowed, await entityManager.AuthorizeAsync(PermissionCatalogue.TaskUpdate, Managed(ProjectId, EntityId, UserId)));
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.TaskUpdate, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.TaskUpdate, Managed(OtherProjectId, EntityId, UserId))).IsAllowed);
    }

    /// <summary>
    /// TASK-048's acceptance criterion: reopening a completed task needs TASK_REOPEN itself. Holding the general edit permissions
    /// over the task — TASK_UPDATE and TASK_MANAGE at ALL — is refused 403, since those let the caller see it (R-47).
    /// </summary>
    [Fact]
    public async Task ReopeningNeedsTheReopenPermissionNotGeneralEditPermission()
    {
        AuthorizationSubject task = Managed(ProjectId, EntityId, OtherUserId) with { AssignedUserIds = [UserId] };
        AuthorizationScenario editor = new AuthorizationScenario(PermissionCatalogue.Platform).WithUser(
            UserType.Internal,
            Grant("R02", PermissionCatalogue.TaskUpdate, DataScope.All),
            Grant("R02", PermissionCatalogue.TaskManage, DataScope.All));
        AuthorizationScenario reopener = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.Internal, Grant("R04", PermissionCatalogue.TaskReopen, DataScope.Assigned));

        Assert.Equal(AuthorizationDecision.Allowed, await editor.AuthorizeAsync(PermissionCatalogue.TaskManage, task));
        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await editor.AuthorizeAsync(PermissionCatalogue.TaskReopen, task));
        Assert.Equal(AuthorizationDecision.Allowed, await reopener.AuthorizeAsync(PermissionCatalogue.TaskReopen, task));
    }

    /// <summary>
    /// TASK-050: the Project Manager views and submits milestone achievement claims at OWN — the projects they manage. Acceptance is
    /// WF-11's APPROVAL_DECIDE, not a milestone permission; the other roles' grants wait for Appendix A (milestone-achievement.md F-2).
    /// </summary>
    [Fact]
    public void MilestoneAchievementsGoToTheProjectManagerOnly()
    {
        Assert.Equal(
            [("R04", PermissionCatalogue.MilestoneView, DataScope.Own), ("R04", PermissionCatalogue.MilestoneSubmit, DataScope.Own)],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("MILESTONE_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    /// <summary>ADR-013's amendment to TASK-050: an entity Project Manager submits achievement claims on their own project, and on no other.</summary>
    [Fact]
    public async Task OnlyTheProjectsOwnManagerSubmitsItsAchievementClaims()
    {
        AuthorizationScenario entityManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R04", PermissionCatalogue.MilestoneSubmit, DataScope.Own, entityId: EntityId, projectId: ProjectId));

        Assert.Equal(AuthorizationDecision.Allowed, await entityManager.AuthorizeAsync(PermissionCatalogue.MilestoneSubmit, Managed(ProjectId, EntityId, UserId)));
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.MilestoneSubmit, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.MilestoneSubmit, Managed(OtherProjectId, EntityId, UserId))).IsAllowed);
    }

    /// <summary>
    /// TASK-052, ADR-013's amendment: an entity sees budget, expenditure and KPI status for its own project — R04 and R08 view
    /// financials and KPIs at ENTITY. Entering, reviewing and source configuration wait for Appendix A (financial-kpi.md F-2).
    /// </summary>
    [Fact]
    public void FinancialsAndKpisAreSeenByTheEntityOnly()
    {
        Assert.Equal(
            [
                ("R04", PermissionCatalogue.FinancialView, DataScope.Entity), ("R08", PermissionCatalogue.FinancialView, DataScope.Entity),
                ("R04", PermissionCatalogue.KpiView, DataScope.Entity), ("R08", PermissionCatalogue.KpiView, DataScope.Entity),
            ],
            PermissionCatalogue.ShippedDefaultGrants
                .Where(g => g.PermissionCode.StartsWith("FINANCIAL_", StringComparison.Ordinal) || g.PermissionCode.StartsWith("KPI_", StringComparison.Ordinal))
                .Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    /// <summary>ADR-013's amendment to TASK-052: an entity user sees the financials of its own entity's projects and of no other entity's.</summary>
    [Fact]
    public async Task AnEntitySeesTheFinancialsOfItsOwnProjectsOnly()
    {
        AuthorizationScenario entityUser = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R08", PermissionCatalogue.FinancialView, DataScope.Entity, entityId: EntityId));

        Assert.Equal(AuthorizationDecision.Allowed, await entityUser.AuthorizeAsync(PermissionCatalogue.FinancialView, Managed(ProjectId, EntityId, OtherUserId)));
        Assert.False((await entityUser.AuthorizeAsync(PermissionCatalogue.FinancialView, Managed(OtherProjectId, OtherEntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityUser.AuthorizeAsync(PermissionCatalogue.FinancialSubmit, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
    }

    /// <summary>
    /// TASK-055, ADR-013's amendment: entity Project Managers register and update risks on their own project — R04 views and manages
    /// risks at OWN. Rating, acceptance and the reopen wait for Appendix A (risk-management.md F-2).
    /// </summary>
    [Fact]
    public void RisksAreRegisteredAndUpdatedByTheProjectManagerOnly()
    {
        Assert.Equal(
            [("R04", PermissionCatalogue.RiskView, DataScope.Own), ("R04", PermissionCatalogue.RiskManage, DataScope.Own)],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("RISK_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    /// <summary>ADR-013's amendment to TASK-055: an entity Project Manager manages the risks of their own project, and of no other.</summary>
    [Fact]
    public async Task OnlyTheProjectsOwnManagerManagesItsRisks()
    {
        AuthorizationScenario entityManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R04", PermissionCatalogue.RiskManage, DataScope.Own, entityId: EntityId, projectId: ProjectId));

        Assert.Equal(AuthorizationDecision.Allowed, await entityManager.AuthorizeAsync(PermissionCatalogue.RiskManage, Managed(ProjectId, EntityId, UserId)));
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.RiskManage, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.RiskManage, Managed(OtherProjectId, EntityId, UserId))).IsAllowed);
    }

    /// <summary>
    /// TASK-055's acceptance criterion: reopening a closed risk needs RISK_REOPEN itself. Holding the general edit permission over every
    /// risk — RISK_MANAGE at ALL — is refused 403, since it lets the caller see the risk (R-47).
    /// </summary>
    [Fact]
    public async Task ReopeningARiskNeedsTheReopenPermissionNotGeneralEditPermission()
    {
        AuthorizationSubject risk = Managed(ProjectId, EntityId, OtherUserId);
        AuthorizationScenario editor = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.Internal, Grant("R02", PermissionCatalogue.RiskView, DataScope.All), Grant("R02", PermissionCatalogue.RiskManage, DataScope.All));
        AuthorizationScenario reopener = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.Internal, Grant("R03", PermissionCatalogue.RiskReopen, DataScope.All));

        Assert.Equal(AuthorizationDecision.Allowed, await editor.AuthorizeAsync(PermissionCatalogue.RiskManage, risk));
        Assert.Equal(AuthorizationDecision.Forbidden(AuthorizationDenial.NotGranted), await editor.AuthorizeAsync(PermissionCatalogue.RiskReopen, risk));
        Assert.Equal(AuthorizationDecision.Allowed, await reopener.AuthorizeAsync(PermissionCatalogue.RiskReopen, risk));
    }

    /// <summary>
    /// TASK-057, ADR-013's amendment: entities raise a blocker or an issue on their own project and see its status — R08 at ENTITY and
    /// R04 at OWN view and raise. WF-07 §3: the Project Manager manages and escalates (R04 at OWN; the application refuses an external
    /// holder both); the Department Manager handles escalations (R03 at DEPT). The rest wait for Appendix A (management-concern.md F-2).
    /// </summary>
    [Fact]
    public void ConcernsAreRaisedByEntitiesManagedByTheProjectManagerAndTheirEscalationsResolvedByTheDepartmentManager()
    {
        Assert.Equal(
            [
                ("R04", PermissionCatalogue.ConcernView, DataScope.Own), ("R04", PermissionCatalogue.ConcernRaise, DataScope.Own),
                ("R04", PermissionCatalogue.ConcernManage, DataScope.Own), ("R04", PermissionCatalogue.ConcernEscalate, DataScope.Own),
                ("R08", PermissionCatalogue.ConcernView, DataScope.Entity), ("R08", PermissionCatalogue.ConcernRaise, DataScope.Entity),
                ("R03", PermissionCatalogue.ConcernView, DataScope.Dept), ("R03", PermissionCatalogue.ConcernEscalationResolve, DataScope.Dept),
            ],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("CONCERN_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));
    }

    /// <summary>ADR-013's amendment to TASK-057: an entity user raises concerns on its own entity's projects and on no other entity's.</summary>
    [Fact]
    public async Task AnEntityRaisesConcernsOnItsOwnProjectsOnly()
    {
        AuthorizationScenario entityUser = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R08", PermissionCatalogue.ConcernRaise, DataScope.Entity, entityId: EntityId));

        Assert.Equal(AuthorizationDecision.Allowed, await entityUser.AuthorizeAsync(PermissionCatalogue.ConcernRaise, Managed(ProjectId, EntityId, OtherUserId)));
        Assert.False((await entityUser.AuthorizeAsync(PermissionCatalogue.ConcernRaise, Managed(OtherProjectId, OtherEntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityUser.AuthorizeAsync(PermissionCatalogue.ConcernEscalate, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
    }

    /// <summary>
    /// TASK-057: an escalation is resolved through the role it is addressed to. CONCERN_ESCALATION_RESOLVE held through another role does
    /// not reach it, so a holder of the permission as R03 is refused an escalation addressed to R02.
    /// </summary>
    [Fact]
    public async Task AnEscalationIsResolvedThroughTheRoleItIsAddressedTo()
    {
        AuthorizationSubject concern = Managed(ProjectId, null, OtherUserId);
        AuthorizationScenario departmentManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.Internal, Grant("R03", PermissionCatalogue.ConcernEscalationResolve, DataScope.Dept));

        Assert.Equal(AuthorizationDecision.Allowed, await departmentManager.Engine.AuthorizeAsync(
            UserId, new AuthorizationRequest(PermissionCatalogue.ConcernEscalationResolve, concern) { RoleCode = "R03" }, CancellationToken.None));
        Assert.False((await departmentManager.Engine.AuthorizeAsync(
            UserId, new AuthorizationRequest(PermissionCatalogue.ConcernEscalationResolve, concern) { RoleCode = "R02" }, CancellationToken.None)).IsAllowed);
    }

    /// <summary>
    /// TASK-060, ADR-013's amendment: entity Project Managers may raise a change request — R04 views and raises at OWN, the projects the
    /// holder manages. WF-08 §3: the Project Manager coordinates implementation (R04 at OWN; the application refuses an external holder),
    /// the Department Manager reviews (R03 at DEPT). Approval is WF-11's; the rest wait for Appendix A (change-request.md F-2).
    /// </summary>
    [Fact]
    public void ChangeRequestsAreRaisedByTheProjectManagerAndReviewedByTheDepartmentManager() =>
        Assert.Equal(
            [
                ("R04", PermissionCatalogue.ChangeRequestView, DataScope.Own), ("R04", PermissionCatalogue.ChangeRequestRaise, DataScope.Own),
                ("R04", PermissionCatalogue.ChangeRequestImplement, DataScope.Own),
                ("R03", PermissionCatalogue.ChangeRequestView, DataScope.Dept), ("R03", PermissionCatalogue.ChangeRequestReview, DataScope.Dept),
            ],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("CHANGE_REQUEST_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));

    /// <summary>ADR-013's amendment to TASK-060: an entity Project Manager raises change requests on the project they manage, and on no other.</summary>
    [Fact]
    public async Task OnlyTheProjectsOwnManagerRaisesItsChangeRequests()
    {
        AuthorizationScenario entityManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R04", PermissionCatalogue.ChangeRequestRaise, DataScope.Own, entityId: EntityId, projectId: ProjectId));

        Assert.Equal(AuthorizationDecision.Allowed, await entityManager.AuthorizeAsync(PermissionCatalogue.ChangeRequestRaise, Managed(ProjectId, EntityId, UserId)));
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.ChangeRequestRaise, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.ChangeRequestReview, Managed(ProjectId, EntityId, UserId))).IsAllowed);
    }

    /// <summary>
    /// WF-09 §3 and ADR-013 (TASK-062): the Project Manager raises suspension and resumption requests (R04 at OWN) and the Department
    /// Manager reviews them (R03 at DEPT). Activation is WF-09's own service, so SUSPENSION_ACTIVATE ships to no role (suspension.md F-2).
    /// </summary>
    [Fact]
    public void SuspensionsAreRaisedByTheProjectManagerReviewedByTheDepartmentManagerAndActivatedByNoRole() =>
        Assert.Equal(
            [
                ("R04", PermissionCatalogue.SuspensionView, DataScope.Own), ("R04", PermissionCatalogue.SuspensionRaise, DataScope.Own),
                ("R03", PermissionCatalogue.SuspensionView, DataScope.Dept), ("R03", PermissionCatalogue.SuspensionReview, DataScope.Dept),
            ],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("SUSPENSION_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));

    /// <summary>ADR-013: an entity Project Manager raises suspension requests on the project they manage, and on no other; review stays AHDA's.</summary>
    [Fact]
    public async Task OnlyTheProjectsOwnManagerRaisesItsSuspensionRequests()
    {
        AuthorizationScenario entityManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R04", PermissionCatalogue.SuspensionRaise, DataScope.Own, entityId: EntityId, projectId: ProjectId));

        Assert.Equal(AuthorizationDecision.Allowed, await entityManager.AuthorizeAsync(PermissionCatalogue.SuspensionRaise, Managed(ProjectId, EntityId, UserId)));
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.SuspensionRaise, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.SuspensionReview, Managed(ProjectId, EntityId, UserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.SuspensionActivate, Managed(ProjectId, EntityId, UserId))).IsAllowed);
    }

    /// <summary>
    /// WF-10 and ADR-013 (TASK-063): the Project Manager raises completion and closure cases (R04 at OWN) and the Department Manager reviews
    /// them and accepts readiness exceptions (R03 at DEPT). Activation is WF-10's own service, so CLOSEOUT_ACTIVATE ships to no role (closure.md F-2).
    /// </summary>
    [Fact]
    public void CloseoutIsRaisedByTheProjectManagerReviewedByTheDepartmentManagerAndActivatedByNoRole() =>
        Assert.Equal(
            [
                ("R04", PermissionCatalogue.CloseoutView, DataScope.Own), ("R04", PermissionCatalogue.CloseoutRaise, DataScope.Own),
                ("R03", PermissionCatalogue.CloseoutView, DataScope.Dept), ("R03", PermissionCatalogue.CloseoutReview, DataScope.Dept),
                ("R03", PermissionCatalogue.CloseoutWaive, DataScope.Dept),
            ],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("CLOSEOUT_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));

    /// <summary>ADR-013: an entity Project Manager raises completion and closure cases on the project they manage, and on no other; review stays AHDA's.</summary>
    [Fact]
    public async Task OnlyTheProjectsOwnManagerRaisesItsCloseoutCases()
    {
        AuthorizationScenario entityManager = new AuthorizationScenario(PermissionCatalogue.Platform)
            .WithUser(UserType.External, Grant("R04", PermissionCatalogue.CloseoutRaise, DataScope.Own, entityId: EntityId, projectId: ProjectId));

        Assert.Equal(AuthorizationDecision.Allowed, await entityManager.AuthorizeAsync(PermissionCatalogue.CloseoutRaise, Managed(ProjectId, EntityId, UserId)));
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.CloseoutRaise, Managed(ProjectId, EntityId, OtherUserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.CloseoutReview, Managed(ProjectId, EntityId, UserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.CloseoutWaive, Managed(ProjectId, EntityId, UserId))).IsAllowed);
        Assert.False((await entityManager.AuthorizeAsync(PermissionCatalogue.CloseoutActivate, Managed(ProjectId, EntityId, UserId))).IsAllowed);
    }

    /// <summary>
    /// WF-13 Path A (TASK-066 gate decision: "direct source action limited to assigned tasks"): an entity user starts, blocks, completes
    /// and reports progress on the WF-04 tasks it owns on its entity's project, and on no other task — not one of its own project owned
    /// by someone else, not one it owns on another entity's project — and never plans, cancels or reopens one.
    /// </summary>
    [Fact]
    public async Task AnEntityUserActsDirectlyOnlyOnTheTasksItOwns()
    {
        AuthorizationScenario entityUser = Scenario("R08");
        AuthorizationSubject ownTask = Managed(ProjectId, EntityId, OtherUserId) with { AssignedUserIds = [UserId] };

        Assert.Equal(AuthorizationDecision.Allowed, await entityUser.AuthorizeAsync(PermissionCatalogue.TaskUpdate, ownTask));
        Assert.False((await entityUser.AuthorizeAsync(PermissionCatalogue.TaskUpdate, ownTask with { AssignedUserIds = [OtherUserId] })).IsAllowed);
        Assert.False((await entityUser.AuthorizeAsync(PermissionCatalogue.TaskUpdate, ownTask with { ExternalEntityId = OtherEntityId })).IsAllowed);
        Assert.False((await entityUser.AuthorizeAsync(PermissionCatalogue.TaskManage, ownTask)).IsAllowed);
        Assert.False((await entityUser.AuthorizeAsync(PermissionCatalogue.TaskReopen, ownTask)).IsAllowed);
    }

    /// <summary>
    /// WF-13 §13 (TASK-066): the Project Manager requests, reviews when assigned and applies (R04 at OWN); the Department Manager requests and
    /// reviews when assigned (R03 at DEPT); the external entity user sees its entity's requests (R08 at ENTITY) and answers those it is named
    /// on (R08 at ASSIGNED). The application refuses requesting, reviewing and applying to an external holder (external-participation.md F-2).
    /// </summary>
    [Fact]
    public void ExternalUpdateRequestsAreRaisedByAhdaAndAnsweredByTheNamedEntityUser() =>
        Assert.Equal(
            [
                ("R04", PermissionCatalogue.ExternalRequestView, DataScope.Own), ("R04", PermissionCatalogue.ExternalRequestManage, DataScope.Own),
                ("R04", PermissionCatalogue.ExternalContributionReview, DataScope.Own), ("R04", PermissionCatalogue.ExternalContributionApply, DataScope.Own),
                ("R03", PermissionCatalogue.ExternalRequestView, DataScope.Dept), ("R03", PermissionCatalogue.ExternalRequestManage, DataScope.Dept),
                ("R03", PermissionCatalogue.ExternalContributionReview, DataScope.Dept),
                ("R08", PermissionCatalogue.ExternalRequestView, DataScope.Entity), ("R08", PermissionCatalogue.ExternalContributionRespond, DataScope.Assigned),
            ],
            PermissionCatalogue.ShippedDefaultGrants.Where(g => g.PermissionCode.StartsWith("EXTERNAL_", StringComparison.Ordinal)).Select(g => (g.RoleCode, g.PermissionCode, g.Scope)));

    /// <summary>
    /// TASK-066 acceptance criterion 1, at the engine: an entity user whose assignment covers one project reaches its own entity's requests on
    /// that project and nothing else — not another entity's request on the same project, not its entity's request on another project — as a
    /// single record (404, R-47) and as a collection's scope alike, and answers only a request it is named on.
    /// </summary>
    [Fact]
    public async Task AnEntityUserReachesOnlyItsOwnEntitysRequestsOnTheProjectItIsAssignedTo()
    {
        AuthorizationScenario entityUser = Scenario("R08");
        AuthorizationSubject own = Managed(ProjectId, EntityId, OtherUserId) with { AssignedUserIds = [UserId] };
        AuthorizationSubject otherEntitysOnTheSameProject = own with { ExternalEntityId = OtherEntityId, AssignedUserIds = [] };
        AuthorizationSubject ownEntitysOnAnotherProject = own with { ProjectId = OtherProjectId, AssignedUserIds = [] };

        Assert.Equal(AuthorizationDecision.Allowed, await entityUser.AuthorizeAsync(PermissionCatalogue.ExternalRequestView, own));
        Assert.Equal(AuthorizationOutcome.NotFound, (await entityUser.AuthorizeAsync(PermissionCatalogue.ExternalRequestView, otherEntitysOnTheSameProject)).Outcome);
        Assert.Equal(AuthorizationOutcome.NotFound, (await entityUser.AuthorizeAsync(PermissionCatalogue.ExternalRequestView, ownEntitysOnAnotherProject)).Outcome);

        RecordScope scope = await entityUser.Engine.GetRecordScopeAsync(UserId, PermissionCatalogue.ExternalRequestView, CancellationToken.None);
        Assert.True(scope.Matches(own));
        Assert.False(scope.Matches(otherEntitysOnTheSameProject));
        Assert.False(scope.Matches(ownEntitysOnAnotherProject));

        Assert.Equal(AuthorizationDecision.Allowed, await entityUser.AuthorizeAsync(PermissionCatalogue.ExternalContributionRespond, own));
        Assert.Equal(AuthorizationOutcome.Forbidden, (await entityUser.AuthorizeAsync(PermissionCatalogue.ExternalContributionRespond, own with { AssignedUserIds = [OtherUserId] })).Outcome);
        foreach (string ahdaOnly in new[] { PermissionCatalogue.ExternalRequestManage, PermissionCatalogue.ExternalContributionReview, PermissionCatalogue.ExternalContributionApply })
        {
            Assert.Equal(AuthorizationDenial.NotGranted, (await entityUser.AuthorizeAsync(ahdaOnly, own)).Denial);
        }
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
