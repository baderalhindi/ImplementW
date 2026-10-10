using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Reports;

/// <summary>
/// FG-02 as the SPA reaches it, and the source rows a test needs. The rows are written as the owning module stores them, with its guards off: FG-02
/// reads what a source stored, through FG-01's register, so a test of FG-02 sets what is stored and never drives the source's workflow.
/// </summary>
internal static class ReportDriver
{
    public const string Reports = "/api/v1/reports";
    public const string Explorer = "/api/v1/report-explorer";
    public const string Allowlist = "/api/v1/report-allowlist-entries";
    public const string Jobs = "/api/v1/report-jobs";
    public const string SavedViews = "/api/v1/saved-views";
    public const string Definitions = "/api/v1/report-definitions";

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    /// <summary>A new project of <paramref name="departmentId"/> (the test department by default), of the active entity unless told otherwise, managed by local.r08.</summary>
    public static async Task<Guid> ReportProjectAsync(
        this ReportTestHost host, string state = "ACTIVE", Guid? departmentId = null, Guid? entityId = null, string title = "Report test project")
    {
        Guid id = Guid.NewGuid();
        string activatedAt = state is "ACTIVE" or "SUSPENDED" or "COMPLETED" or "CLOSED" ? "now() - interval '30 days'" : "NULL";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', 'PRJ-R{id.ToString("N")[..12]}', '{title.Replace("'", "''", StringComparison.Ordinal)}', 'en', '{ReportTestHost.ClassificationId}',
                    '{departmentId ?? ReportTestHost.DepartmentId}', '{entityId ?? ReportTestHost.EntityId}', '{Person(8)}', '{state}', '{host.StandardProfileId}',
                    'ENTITY_MANAGED', {activatedAt}, now(), '{Seed}', now(), '{Seed}')
            """);
        return id;
    }

    /// <summary>A WF-02 reporting period: OPEN until its progress is published, then CLOSED.</summary>
    public static async Task<Guid> ReportingPeriodAsync(this ReportTestHost host, Guid projectId, DateOnly start, DateOnly due, string status)
    {
        Guid id = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO progress.reporting_cycle (id, project_id, period_start, period_end, due_date, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', '{projectId}', '{Iso(start)}', '{Iso(start.AddDays(6))}', '{Iso(due)}', '{status}', now(), '{Seed}', now(), '{Seed}')
            """);
        return id;
    }

    /// <summary>A Published Progress Snapshot of the period, as WF-02 writes one at publication.</summary>
    public static Task PublishedSnapshotAsync(this ReportTestHost host, Guid projectId, Guid cycleId, string overallHealth, decimal actual, DateTimeOffset publishedAt) =>
        host.Database.ExecuteAsync(Unguarded($"""
            INSERT INTO progress.published_progress_snapshot (id, project_id, reporting_cycle_id, progress_submission_id, published_at, published_by_user_id, actual_percent,
                                                              is_overridden, planned_percent, overall_health, schedule_health, financial_status, health_rule_configuration_version_id,
                                                              created_at, created_by, updated_at, updated_by)
            VALUES ('{Guid.NewGuid()}', '{projectId}', '{cycleId}', '{Guid.NewGuid()}', '{Utc(publishedAt)}', '{Person(2)}', {Number(actual)}, false, 50,
                    '{overallHealth}', 'GREEN', NULL, '{Guid.NewGuid()}', now(), '{Seed}', now(), '{Seed}')
            """));

    /// <summary>A Published Financial Snapshot, as WF-14 writes one: figures exactly when MEASURED, a budget with the commitment it is.</summary>
    public static Task PublishedFinancialsAsync(this ReportTestHost host, Guid projectId, Guid cycleId, string budget, string actual, string forecast) =>
        host.Database.ExecuteAsync(Unguarded($"""
            INSERT INTO financial_kpi.published_financial_snapshot (id, project_id, reporting_cycle_id, financial_progress_update_id, financial_commitment_id, published_at, published_by_user_id,
                                                                    approved_budget_sar, actual_expenditure_to_date_sar, forecast_at_completion_sar, value_status, financial_status,
                                                                    threshold_configuration_version_id, source_type, as_of_date, entered_by_user_id,
                                                                    created_at, created_by, updated_at, updated_by)
            VALUES ('{Guid.NewGuid()}', '{projectId}', '{cycleId}', '{Guid.NewGuid()}', '{Guid.NewGuid()}', now(), '{Person(2)}', {budget}, {actual}, {forecast},
                    'MEASURED', 'GREEN', '{Guid.NewGuid()}', 'MANUAL', '{Iso(Today.AddDays(-3))}', '{Person(8)}', now(), '{Seed}', now(), '{Seed}')
            """));

    public static Task<HttpResponseMessage> RunAsync(this HttpClient client, string token, string code, object body, string query = "") =>
        client.PostAsync($"{Reports}/{code}/run{query}", token, body);

    public static async Task<JsonObject> RunOrFailAsync(this HttpClient client, string token, string code, object body, string query = "")
    {
        using HttpResponseMessage response = await client.RunAsync(token, code, body, query);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"run {code}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    /// <summary>An export, with the key given or a fresh one.</summary>
    public static Task<HttpResponseMessage> ExportAsync(this HttpClient client, string token, string path, object body, string? key = null) =>
        client.SendAsync(HttpMethod.Post, path, token, body, idempotencyKey: key);

    public static async Task<Guid> ExportOrFailAsync(this HttpClient client, string token, string path, object body)
    {
        using HttpResponseMessage response = await client.ExportAsync(token, path, body);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, $"export: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        Assert.StartsWith(Jobs + "/", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
        return Guid.Parse((await response.ReadObjectAsync())["id"]!.GetValue<string>());
    }

    public static async Task<JsonObject> JobOrFailAsync(this HttpClient client, string token, Guid jobId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{Jobs}/{jobId}", token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"job: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    public static Task<HttpResponseMessage> DownloadAsync(this HttpClient client, string token, Guid jobId) => client.GetAsync($"{Jobs}/{jobId}/content", token);

    public static async Task<(HttpStatusCode Status, string? Code, string[] Errors)> RefusalAsync(this HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return (response.StatusCode, null, []);
        }

        JsonObject problem = await response.ReadObjectAsync();
        return (response.StatusCode, problem["code"]?.GetValue<string>(),
            [.. (problem["errors"]?.AsArray() ?? []).Select(e => $"{e!["field"]} {e["code"]}")]);
    }

    /// <summary>The cell of the result row of <paramref name="projectId"/> in the column named <paramref name="entity"/>.<paramref name="field"/>.</summary>
    public static JsonObject Cell(this JsonObject result, Guid projectId, string entity, string field)
    {
        int column = result["columns"]!.AsArray().Select((c, i) => (c, i))
            .Single(x => x.c!["sourceEntityCode"]!.GetValue<string>() == entity && x.c["fieldCode"]!.GetValue<string>() == field).i;
        return result["items"]!.AsArray().Single(r => r!["projectId"]!.GetValue<string>() == projectId.ToString())!["cells"]!.AsArray()[column]!.AsObject();
    }

    public static IReadOnlyList<Guid> ProjectIds(this JsonObject result) =>
        [.. result["items"]!.AsArray().Select(r => Guid.Parse(r!["projectId"]!.GetValue<string>()))];

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Utc(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00", CultureInfo.InvariantCulture);

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The statement with the source's guard triggers off, in a transaction of its own, as a fixture writes a stored fact.</summary>
    private static string Unguarded(string sql) => $"BEGIN; SET LOCAL session_replication_role = replica; {sql}; COMMIT;";
}
