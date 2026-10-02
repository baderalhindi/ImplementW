using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>WF-02 as the SPA reaches it, and the fixtures a test needs that no API creates yet.</summary>
internal static class ProgressDriver
{
    public const string Submissions = "/api/v1/progress-submissions";
    public const string Cycles = "/api/v1/reporting-cycles";
    public const string Snapshots = "/api/v1/published-progress-snapshots";
    public const string HealthStatuses = "/api/v1/project-health-statuses";

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// A new project in <paramref name="state"/>, of the test department and — unless <paramref name="entity"/> is false — of the
    /// active entity, managed by <paramref name="projectManager"/> and activated <paramref name="activatedDaysAgo"/> days ago.
    /// </summary>
    public static async Task<Guid> ProjectAsync(
        this ProgressTestHost host, int projectManager = 8, int activatedDaysAgo = 3, bool entity = true, DateOnly? legacyIntakeDate = null, string state = "ACTIVE")
    {
        Guid id = Guid.NewGuid();
        string activatedAt = state == "ACTIVE" ? $"now() - interval '{activatedDaysAgo} days'" : "NULL";
        string intake = legacyIntakeDate is { } date ? $"'{date:yyyy-MM-dd}'" : "NULL";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, legacy_intake_date,
                                         created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', 'PRJ-T{id.ToString("N")[..12]}', 'Progress test project', 'en', '{ProgressTestHost.ClassificationId}', '{ProgressTestHost.DepartmentId}',
                    {(entity ? $"'{ProgressTestHost.EntityId}'" : "NULL")}, '{Person(projectManager)}', '{state}', '{host.GovernanceProfileId}',
                    '{(entity ? "ENTITY_MANAGED" : "AHDA_MANAGED")}', {activatedAt}, {intake}, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """);
        return id;
    }

    public static Task<HttpResponseMessage> StartAsync(this HttpClient client, string token, Guid projectId) =>
        client.PostAsync(Submissions, token, new { projectId });

    /// <summary>Starts the project's next period and returns the DRAFT, asserting 201.</summary>
    public static async Task<JsonObject> StartOrFailAsync(this HttpClient client, string token, Guid projectId)
    {
        using HttpResponseMessage started = await client.StartAsync(token, projectId);
        Assert.True(started.StatusCode == HttpStatusCode.Created, $"start: {(int)started.StatusCode} {await started.Content.ReadAsStringAsync()}");
        return await started.ReadObjectAsync();
    }

    public static Task<HttpResponseMessage> CommandAsync(this HttpClient client, string token, Guid submissionId, string command, object? body = null) =>
        client.PostAsync($"{Submissions}/{submissionId}/{command}", token, body);

    /// <summary>Sends the command and returns the submission, asserting 200.</summary>
    public static async Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, Guid submissionId, string command, object? body = null)
    {
        using HttpResponseMessage response = await client.CommandAsync(token, submissionId, command, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    /// <summary>Replaces the DRAFT's narrative and override under its current ETag.</summary>
    public static async Task<HttpResponseMessage> EditAsync(this HttpClient client, string token, Guid submissionId, object body)
    {
        using HttpResponseMessage current = await client.GetAsync($"{Submissions}/{submissionId}", token);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        return await client.PutAsync($"{Submissions}/{submissionId}", token, body, AdministrationApi.ETagOf(current));
    }

    /// <summary>A period started and submitted by <paramref name="submitter"/>, then reviewed and published by <paramref name="reviewer"/>.</summary>
    public static async Task<JsonObject> PublishedPeriodAsync(this HttpClient client, string submitter, string reviewer, Guid projectId, object? edit = null)
    {
        Guid id = AdministrationApi.IdOf(await client.StartOrFailAsync(submitter, projectId));
        if (edit is not null)
        {
            using HttpResponseMessage edited = await client.EditAsync(submitter, id, edit);
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        await client.CommandOrFailAsync(submitter, id, "submit");
        await client.CommandOrFailAsync(reviewer, id, "start-review");
        return await client.CommandOrFailAsync(reviewer, id, "publish");
    }

    /// <summary>The project's items of a WF-02 collection, first page.</summary>
    public static async Task<JsonArray> ItemsAsync(this HttpClient client, string token, string collection, Guid projectId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{collection}?projectId={projectId}&pageSize=200", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadObjectAsync())["items"]!.AsArray();
    }

    /// <summary>The status and the R-27 code of a refusal.</summary>
    public static async Task<(HttpStatusCode Status, string? Code)> RefusalAsync(this HttpResponseMessage response) =>
        (response.StatusCode, (await response.ReadObjectAsync())["code"]?.GetValue<string>());

    public static string Status(this JsonNode node) => node["status"]!.GetValue<string>();

    public static decimal Percent(this JsonNode node, string property) => node[property]!.GetValue<decimal>();

    /// <summary>A row as the database holds it, every column, as JSON text: what "unchanged" is compared on.</summary>
    public static async Task<string> RowAsync(this ProgressTestHost host, string table, Guid id) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT to_jsonb(r)::text FROM progress.{table} r WHERE r.id = '{id}'"));

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this ProgressTestHost host, Guid subjectId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'Progress' AND subject_id = '{subjectId}' ORDER BY occurred_at, id");

    public static async Task<ProgressSessions> SignInAsync(this HttpClient client) =>
        new((await client.SignInOrFailAsync(8)).AccessToken, (await client.SignInOrFailAsync(3)).AccessToken, (await client.SignInOrFailAsync(2)).AccessToken);
}

/// <summary>The people most tests act as: the entity Project Manager (local.r08), the department reviewer (local.r03), and local.r02.</summary>
internal sealed record ProgressSessions(string ProjectManager, string Reviewer, string Portfolio);
