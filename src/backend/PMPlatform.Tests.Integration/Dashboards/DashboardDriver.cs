using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Dashboards;

/// <summary>
/// FG-01 as the SPA reaches it, and the source rows a test needs. The rows are written as the owning module stores them, with its guards off:
/// FG-01 reads what a source stored, so a test of FG-01 sets what is stored and never drives the source's workflow.
/// </summary>
internal static class DashboardDriver
{
    public const string Dashboards = "/api/v1/dashboards";
    public const string Definitions = "/api/v1/dashboard-definitions";
    public const string Projections = "/api/v1/dashboard-projections";

    private const string Seed = IdentityDatabase.SeedPrincipalId;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    /// <summary>A new project of <paramref name="departmentId"/> (the test department by default), of the active entity unless <paramref name="entityId"/> says otherwise, managed by local.r08.</summary>
    public static async Task<Guid> DashboardProjectAsync(this DashboardTestHost host, string state = "ACTIVE", Guid? departmentId = null, Guid? entityId = null)
    {
        Guid id = Guid.NewGuid();
        string activatedAt = state is "ACTIVE" or "SUSPENDED" or "COMPLETED" or "CLOSED" ? "now() - interval '30 days'" : "NULL";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', 'PRJ-D{id.ToString("N")[..12]}', 'Dashboard test project', 'en', '{DashboardTestHost.ClassificationId}', '{departmentId ?? DashboardTestHost.DepartmentId}',
                    '{entityId ?? DashboardTestHost.EntityId}', '{Person(8)}', '{state}', '{host.StandardProfileId}', 'ENTITY_MANAGED', {activatedAt},
                    now(), '{Seed}', now(), '{Seed}')
            """);
        return id;
    }

    /// <summary>A WF-02 reporting period: OPEN until its progress is published, then CLOSED.</summary>
    public static async Task<Guid> ReportingPeriodAsync(this DashboardTestHost host, Guid projectId, DateOnly start, DateOnly due, string status)
    {
        Guid id = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO progress.reporting_cycle (id, project_id, period_start, period_end, due_date, status, created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', '{projectId}', '{Iso(start)}', '{Iso(start.AddDays(6))}', '{Iso(due)}', '{status}', now(), '{Seed}', now(), '{Seed}')
            """);
        return id;
    }

    /// <summary>A Published Progress Snapshot of the period, as WF-02 writes one at publication.</summary>
    public static Task PublishedSnapshotAsync(
        this DashboardTestHost host, Guid projectId, Guid cycleId, string overallHealth, string? scheduleHealth, decimal actual, decimal? planned, DateTimeOffset publishedAt) =>
        host.Database.ExecuteAsync(Unguarded($"""
            INSERT INTO progress.published_progress_snapshot (id, project_id, reporting_cycle_id, progress_submission_id, published_at, published_by_user_id, actual_percent,
                                                              is_overridden, planned_percent, overall_health, schedule_health, financial_status, health_rule_configuration_version_id,
                                                              created_at, created_by, updated_at, updated_by)
            VALUES ('{Guid.NewGuid()}', '{projectId}', '{cycleId}', '{Guid.NewGuid()}', '{Utc(publishedAt)}', '{Person(2)}', {Number(actual)}, false, {Number(planned)},
                    '{overallHealth}', {Text(scheduleHealth)}, NULL, '{Guid.NewGuid()}', now(), '{Seed}', now(), '{Seed}')
            """));

    /// <summary>WF-02's CURRENT/LIVE Overall Project Health row.</summary>
    public static Task LiveHealthAsync(this DashboardTestHost host, Guid projectId, string overallHealth, DateTimeOffset computedAt) =>
        host.Database.ExecuteAsync(Unguarded($"""
            INSERT INTO progress.project_health_status (id, project_id, overall_health, actual_percent, planned_percent, computed_at, health_rule_configuration_version_id,
                                                        created_at, created_by, updated_at, updated_by)
            VALUES ('{Guid.NewGuid()}', '{projectId}', '{overallHealth}', 40, 50, '{Utc(computedAt)}', '{Guid.NewGuid()}', now(), '{Seed}', now(), '{Seed}')
            """));

    /// <summary>WF-03's live Schedule Health row; a variance is measured against a baseline, so one is named with it.</summary>
    public static Task ScheduleHealthAsync(this DashboardTestHost host, Guid projectId, string scheduleHealth, int? finishVarianceDays, DateTimeOffset computedAt) =>
        host.Database.ExecuteAsync(Unguarded($"""
            INSERT INTO schedule.schedule_health_status (id, project_id, project_baseline_id, schedule_health, finish_variance_days, computed_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{Guid.NewGuid()}', '{projectId}', {(finishVarianceDays is null ? "NULL" : $"'{Guid.NewGuid()}'")}, '{scheduleHealth}',
                    {finishVarianceDays?.ToString(CultureInfo.InvariantCulture) ?? "NULL"}, '{Utc(computedAt)}', now(), '{Seed}', now(), '{Seed}')
            """));

    /// <summary>A WF-06 risk identified and not yet assessed, its review due <paramref name="nextReviewDate"/>.</summary>
    public static Task OpenRiskAsync(this DashboardTestHost host, Guid projectId, DateOnly? nextReviewDate = null) =>
        host.Database.ExecuteAsync(Unguarded($"""
            INSERT INTO risk.risk (id, project_id, title, title_lang, description, description_lang, risk_category_item_id, status, identified_date, next_review_date,
                                   reopened_count, created_at, created_by, updated_at, updated_by)
            VALUES ('{Guid.NewGuid()}', '{projectId}', 'Dashboard test risk', 'en', 'A risk.', 'en', '{Guid.NewGuid()}', 'IDENTIFIED', '{Iso(Today.AddDays(-10))}',
                    {(nextReviewDate is { } d ? $"'{Iso(d)}'" : "NULL")}, 0, now(), '{Seed}', now(), '{Seed}')
            """));

    /// <summary>A Published Financial Snapshot of the period, as WF-14 writes one: figures exactly when MEASURED, a budget with the commitment it is.</summary>
    public static Task PublishedFinancialsAsync(
        this DashboardTestHost host, Guid projectId, Guid cycleId, string valueStatus, string financialStatus, string? budget, string? actual, string? forecast, DateOnly asOf) =>
        host.Database.ExecuteAsync(Unguarded($"""
            INSERT INTO financial_kpi.published_financial_snapshot (id, project_id, reporting_cycle_id, financial_progress_update_id, financial_commitment_id, published_at, published_by_user_id,
                                                                    approved_budget_sar, actual_expenditure_to_date_sar, forecast_at_completion_sar, value_status, financial_status,
                                                                    threshold_configuration_version_id, source_type, as_of_date, entered_by_user_id,
                                                                    created_at, created_by, updated_at, updated_by)
            VALUES ('{Guid.NewGuid()}', '{projectId}', '{cycleId}', '{Guid.NewGuid()}', {(budget is null ? "NULL" : $"'{Guid.NewGuid()}'")}, now(), '{Person(2)}',
                    {budget ?? "NULL"}, {actual ?? "NULL"}, {forecast ?? "NULL"},
                    '{valueStatus}', '{financialStatus}', '{Guid.NewGuid()}', 'MANUAL', '{Iso(asOf)}', '{Person(8)}', now(), '{Seed}', now(), '{Seed}')
            """));

    public static async Task<JsonObject> DashboardOrFailAsync(this HttpClient client, string token, string code, string query = "")
    {
        using HttpResponseMessage response = await client.GetAsync($"{Dashboards}/{code}{query}", token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {code}{query}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    public static async Task<(HttpStatusCode Status, string? Code)> DashboardRefusalAsync(this HttpResponseMessage response) =>
        (response.StatusCode, response.Content.Headers.ContentLength == 0 ? null : (await response.ReadObjectAsync())["code"]?.GetValue<string>());

    public static JsonObject Widget(this JsonObject dashboard, string code) =>
        dashboard["widgets"]!.AsArray().Single(w => w!["code"]!.GetValue<string>() == code)!.AsObject();

    public static string Meta(this JsonObject widget, string property) => widget["projection"]![property]!.GetValue<string>();

    public static string? Reason(this JsonObject widget) => widget["unknownReason"]?.GetValue<string>();

    public static IReadOnlyDictionary<string, int> Buckets(this JsonObject widget) =>
        widget["data"]!["distribution"]!.AsArray().ToDictionary(b => b!["key"]!.GetValue<string>(), b => b!["count"]!.GetValue<int>());

    public static string? Figure(this JsonObject widget, string measure) =>
        widget["data"]!["figures"]!.AsArray().Single(f => f!["measure"]!.GetValue<string>() == measure)!["value"]?.GetValue<string>();

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Utc(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00", CultureInfo.InvariantCulture);

    private static string Number(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "NULL";

    private static string Text(string? value) => value is null ? "NULL" : $"'{value}'";

    /// <summary>The statement with the source's guard triggers off, in a transaction of its own, as a fixture writes a stored fact.</summary>
    private static string Unguarded(string sql) => $"BEGIN; SET LOCAL session_replication_role = replica; {sql}; COMMIT;";
}
