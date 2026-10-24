using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Project;

namespace PMPlatform.Tests.Integration.DataImport;

/// <summary>
/// TASK-102 on WF-01: a project already under way is taken in by ADR-014's intake — ACTIVE, with its Formal Project ID and manager — and
/// once the intake is dispatched each module has written its own fact: the Declared Baseline, the opening percentage, the declared budget
/// and the opening spend. A manager without R04 over the project is given it on the project; role assignments load after projects.
/// </summary>
[Collection(ProjectSuite.Name)]
public sealed class ProjectImportTests(ProjectTestHost host)
{
    private const string Projects = "/api/v1/projects";
    private const string Assignments = "/api/v1/access-relationships";

    private static readonly string[] Header =
    [
        "legacy_id", "formal_project_id", "title", "description", "text_language", "classification_code", "department_code", "external_entity_code",
        "participation_mode", "governance_profile_code", "project_manager_username", "manager_sponsor_username", "registration_budget_sar",
        "planned_start_date", "planned_end_date", "region_code", "city_code", "latitude", "longitude", "intake_date", "declared_scope",
        "declared_budget_sar", "declared_end_date", "opening_percent_complete", "opening_spend_to_date_sar",
    ];

    [Fact]
    public async Task AProjectUnderWayIsTakenInActiveAndEachModuleWritesItsOpeningFact()
    {
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        byte[] workbook = ImportApi.Workbook(Header,
            Row($"P{run}-1", $"LEG-{run}-1", "local.r05", entity: null, mode: "AHDA_MANAGED", region: "TEST_REGION", city: "TEST_CITY"),
            Row($"P{run}-2", null, "local.r08", entity: "ENT-IDENTITY-ACTIVE", mode: "ENTITY_MANAGED"));

        using HttpResponseMessage response = await client.UploadAsync(Projects, administrator.AccessToken, workbook, commit: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonObject report = await response.ReadObjectAsync();
        Assert.Empty(ImportApi.IssuesOf(report));
        string[] ids = [.. report["rows"]!.AsArray().Select(r => r!["entityId"]!.GetValue<string>())];
        Assert.Equal(
            [$"LEG-{run}-1|ACTIVE|local.r05|{Today}|true|-", $"PRJ-|ACTIVE|local.r08|{Today}|true|ENT-IDENTITY-ACTIVE"],
            await host.Database.QueryAsync($"""
                SELECT CASE WHEN p.formal_project_id LIKE 'PRJ-%' THEN 'PRJ-' ELSE p.formal_project_id END || '|' || p.lifecycle_state || '|' || u.username || '|'
                       || p.legacy_intake_date || '|' || (p.activated_at IS NOT NULL) || '|' || coalesce(e.code, '-')
                FROM project.project p JOIN identity_access."user" u ON u.id = p.project_manager_user_id
                LEFT JOIN identity_access.external_entity e ON e.id = p.external_entity_id
                WHERE p.id IN ('{ids[0]}', '{ids[1]}') ORDER BY p.created_at, p.id
                """));
        Assert.Equal(["Project.ProjectTakenIn"], await host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_id = '{ids[0]}'"));

        foreach (string projectId in ids)
        {
            Guid message = Guid.Parse(Assert.Single(await host.Database.QueryAsync($"""
                SELECT m.id::text FROM common.outbox_message m JOIN project.project_intake i ON m.message_key = 'Project.ProjectIntakeRecorded:' || i.id
                WHERE i.project_id = '{projectId}'
                """)));
            Assert.True(await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(message, CancellationToken.None));
        }

        Assert.Equal(
            [$"DECLARED|ACTIVE|2027-12-31|SUBMITTED|35.0000|DECLARED_BUDGET|ACTIVE|1250000.00|SUBMITTED|400000.00|MEASURED"],
            await host.Database.QueryAsync($"""
                SELECT b.baseline_type || '|' || b.status || '|' || b.declared_end_date || '|' || s.status || '|' || s.actual_percent_calculated || '|'
                       || c.commitment_type || '|' || c.status || '|' || c.amount_sar || '|' || f.status || '|' || f.actual_expenditure_to_date_sar || '|' || f.value_status
                FROM project.project_intake i
                JOIN schedule.project_baseline b ON b.project_intake_id = i.id
                JOIN progress.progress_submission s ON s.project_intake_id = i.id
                JOIN financial_kpi.financial_commitment c ON c.project_intake_id = i.id
                JOIN financial_kpi.financial_progress_update f ON f.project_intake_id = i.id AND f.reporting_cycle_id = s.reporting_cycle_id
                WHERE i.project_id = '{ids[0]}'
                """));

        // A redelivered intake writes nothing twice.
        Guid again = Guid.Parse(Assert.Single(await host.Database.QueryAsync($"""
            UPDATE common.outbox_message m SET dispatched_at = NULL FROM project.project_intake i
            WHERE m.message_key = 'Project.ProjectIntakeRecorded:' || i.id AND i.project_id = '{ids[0]}' RETURNING m.id::text
            """)));
        Assert.True(await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(again, CancellationToken.None));
        Assert.Equal(["1|1|1"], await host.Database.QueryAsync($"""
            SELECT (SELECT count(*) FROM schedule.project_baseline WHERE project_id = '{ids[0]}') || '|'
                   || (SELECT count(*) FROM progress.progress_submission WHERE project_id = '{ids[0]}') || '|'
                   || (SELECT count(*) FROM financial_kpi.financial_commitment WHERE project_id = '{ids[0]}')
            """));
    }

    [Fact]
    public async Task AManagerWithoutR04OverTheProjectIsGivenItOnTheProject()
    {
        string run = Run();
        string manager = await CreateUserAsync(run);
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);

        JsonObject report = await (await client.UploadAsync(Projects, administrator.AccessToken,
            ImportApi.Workbook(Header, Row($"Q{run}-1", null, manager, entity: null, mode: "AHDA_MANAGED")), commit: true)).ReadObjectAsync();

        Assert.Empty(ImportApi.IssuesOf(report));
        string projectId = report["rows"]![0]!["entityId"]!.GetValue<string>();
        Assert.Equal(
            ["R04-DEFAULT|ACTIVE"],
            await host.Database.QueryAsync($"""
                SELECT pp.code || '|' || a.status FROM identity_access.access_relationship a
                JOIN identity_access.permission_profile_version v ON v.id = a.permission_profile_version_id
                JOIN identity_access.permission_profile pp ON pp.id = v.permission_profile_id
                JOIN identity_access."user" u ON u.id = a.user_id
                WHERE u.username = '{manager}' AND a.project_id = '{projectId}'
                """));
    }

    [Fact]
    public async Task AProjectRowIsHeldToTheRegistrationRulesAndNothingIsTakenIn()
    {
        string run = Run();
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        string?[] good = Row($"R{run}-1", null, "local.r05", entity: null, mode: "AHDA_MANAGED");
        string?[] unknown = Row($"R{run}-2", null, "nobody", entity: null, mode: "AHDA_MANAGED");
        unknown[5] = "NO_SUCH_CLASS";
        unknown[6] = "NO-SUCH-DEPT";
        string?[] shape = Row($"R{run}-3", null, "local.r05", entity: null, mode: "AHDA_MANAGED");
        shape[13] = "2026-06-01";
        shape[14] = "2026-01-01";
        shape[19] = "2999-01-01";
        shape[23] = "120";
        string?[] noEntity = Row($"R{run}-4", null, "local.r05", entity: null, mode: "ENTITY_MANAGED");
        string?[] draftClass = Row($"R{run}-5", null, "local.r05", entity: null, mode: "AHDA_MANAGED");
        draftClass[5] = "TEST_DRAFT_CLASS";

        JsonObject report = await (await client.UploadAsync(Projects, administrator.AccessToken,
            ImportApi.Workbook(Header, good, unknown, shape, noEntity, draftClass), commit: true)).ReadObjectAsync();

        // A DRAFT classification is not offered, so it does not resolve: only PUBLISHED items may be named.
        Assert.Equal(
            [
                "3 classification_code REFERENCE_NOT_FOUND", "3 department_code REFERENCE_NOT_FOUND", "3 project_manager_username REFERENCE_NOT_FOUND",
                "4 planned_end_date DATE_BEFORE_START", "4 intake_date OUT_OF_RANGE", "4 opening_percent_complete OUT_OF_RANGE",
                "5 external_entity_code REQUIRED", "6 classification_code REFERENCE_NOT_FOUND",
            ],
            ImportApi.IssuesOf(report));
        Assert.False(report["committed"]!.GetValue<bool>());
        Assert.Equal(["0"], await host.Database.QueryAsync($"SELECT count(*)::text FROM project.project WHERE title LIKE 'Imported {run}%'"));
    }

    [Fact]
    public async Task RoleAssignmentsLoadAfterProjectsThroughAdm010sRules()
    {
        string run = Run();
        string person = await CreateUserAsync(run);
        using HttpClient client = host.Api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        await client.UploadAsync(Projects, administrator.AccessToken, ImportApi.Workbook(Header, Row($"S{run}-1", null, "local.r05", entity: null, mode: "AHDA_MANAGED")), commit: true);
        string[] header = ["legacy_id", "username", "profile_code", "department_code", "external_entity_code", "project_legacy_id", "sponsor_username", "ends_on"];
        byte[] workbook = ImportApi.Workbook(header,
            [$"A{run}-1", person, "R03-DEFAULT", "DEPT-IDENTITY-TEST", null, null, null, "2099-12-31"],
            [$"A{run}-2", person, "R06-DEFAULT", null, null, $"S{run}-1", null, null],
            [$"A{run}-3", "local.r01", "R06-DEFAULT", null, null, null, null, null],
            [$"A{run}-4", person, "NO-SUCH-PROFILE", null, null, "NO-SUCH-PROJECT", null, null]);

        JsonObject refused = await (await client.UploadAsync(Assignments, administrator.AccessToken, workbook, commit: true)).ReadObjectAsync();

        Assert.Equal(
            ["4 username IDENTITY_ACCESS_SELF_ADMINISTRATION", "5 profile_code REFERENCE_NOT_FOUND", "5 project_legacy_id REFERENCE_NOT_FOUND"],
            ImportApi.IssuesOf(refused).Select(i => i.Replace(" NOT_ALLOWED", " IDENTITY_ACCESS_SELF_ADMINISTRATION", StringComparison.Ordinal)));

        JsonObject imported = await (await client.UploadAsync(Assignments, administrator.AccessToken,
            ImportApi.Workbook(header, [$"A{run}-1", person, "R03-DEFAULT", "DEPT-IDENTITY-TEST", null, null, null, "2099-12-31"], [$"A{run}-2", person, "R06-DEFAULT", null, null, $"S{run}-1", null, null]),
            commit: true)).ReadObjectAsync();

        Assert.Empty(ImportApi.IssuesOf(imported));
        Assert.Equal(
            ["R03-DEFAULT|DEPT-IDENTITY-TEST|-|2100-01-01", "R06-DEFAULT|-|project|-"],
            await host.Database.QueryAsync($"""
                SELECT pp.code || '|' || coalesce(d.code, '-') || '|' || CASE WHEN a.project_id IS NULL THEN '-' ELSE 'project' END || '|'
                       || coalesce(to_char(a.ends_at AT TIME ZONE 'UTC', 'YYYY-MM-DD'), '-')
                FROM identity_access.access_relationship a
                JOIN identity_access.permission_profile_version v ON v.id = a.permission_profile_version_id
                JOIN identity_access.permission_profile pp ON pp.id = v.permission_profile_id
                LEFT JOIN identity_access.department d ON d.id = a.department_id
                JOIN identity_access."user" u ON u.id = a.user_id
                WHERE u.username = '{person}' ORDER BY pp.code
                """));
    }

    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string?[] Row(string legacyId, string? formalId, string manager, string? entity, string mode, string? region = null, string? city = null) =>
    [
        legacyId, formalId, $"Imported {legacyId[1..]} project", "Carried over from the legacy register", "en", "IDENTITY_TEST", "DEPT-IDENTITY-TEST", entity,
        mode, "STANDARD", manager, null, "1500000.00", "2025-01-01", "2027-12-31", region, city, null, null, Today, "Ring road phase 2 as contracted",
        "1250000.00", "2027-12-31", "35", "400000",
    ];

    /// <summary>An active internal user holding no role, so the test's assignments are the only ones it has.</summary>
    private async Task<string> CreateUserAsync(string run)
    {
        string username = $"imp.{run.ToLowerInvariant()}";
        await host.Database.ExecuteAsync($"""
            INSERT INTO identity_access."user" (id, user_type, directory_subject_id, username, display_name, email, department_id, status, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), 'INTERNAL', 'SUBJ-{run}', '{username}', 'Imported {run}', '{username}@ahda.test', '{IdentityDatabase.DepartmentId}', 'ACTIVE',
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """);
        return username;
    }

    private static string Run() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
