using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Suspension;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Suspension;

/// <summary>
/// WF-09 as the SPA reaches it; WF-03 as the Project Manager keeps the schedule; WF-11's decisions as local.r02 makes them, delivered by the
/// outbox when a test says; WF-09's activation pass when a test runs it; and the fixtures no API creates.
/// </summary>
internal static class SuspensionDriver
{
    public const string Requests = "/api/v1/suspension-requests";
    public const string Suspensions = "/api/v1/active-suspensions";
    public const string Schedules = "/api/v1/project-schedules";
    public const string Activities = "/api/v1/schedule-activities";
    public const string Baselines = "/api/v1/project-baselines";

    /// <summary>The first day of every test plan.</summary>
    public static readonly DateOnly Day1 = new(2027, 1, 3);

    /// <summary>The API's today: the UTC date.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A new ACTIVE project of the test department and the active entity, managed by local.r08, under <paramref name="profile"/>.</summary>
    public static async Task<Guid> ActiveProjectAsync(this SuspensionTestHost host, string profile = "LIGHT")
    {
        Guid id = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            SELECT '{id}', 'PRJ-S{id.ToString("N")[..12]}', 'Suspension test project', 'en', '{SuspensionTestHost.ClassificationId}', '{SuspensionTestHost.DepartmentId}',
                   '{SuspensionTestHost.EntityId}', '{Person(8)}', 'ACTIVE', i.id, 'ENTITY_MANAGED', now(),
                   now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = '{profile}'
            """);
        return id;
    }

    /// <summary>
    /// WF-03 on a LIGHT project: a schedule of one activity of 20 working days from <see cref="Day1"/>, baselined by its Project Manager at
    /// once (ADR-015). Returns the ACTIVE Approved Baseline.
    /// </summary>
    public static async Task<Guid> BaselinedAsync(this HttpClient client, SuspensionSessions sessions, Guid projectId)
    {
        await client.CreatedOrFailAsync(sessions.EntityManager, Schedules, new { projectId });
        await client.CreatedOrFailAsync(sessions.EntityManager, Activities, new
        {
            projectId,
            wbsCode = "1",
            name = new { text = "Works", language = "en" },
            requestedStartDate = Iso(Day1),
            plannedDurationDays = 20,
        });
        Guid candidate = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, Baselines, new { projectId }));
        JsonObject baseline = await client.OkOrFailAsync(sessions.EntityManager, $"{Baselines}/{candidate}/submit", new { changeAuthorizationId = (Guid?)null });
        Assert.Equal("ACTIVE", baseline.Text("status"));
        return candidate;
    }

    /// <summary>A request's body: the type, a plain reason, the effective date and, for a suspension, the planned resumption.</summary>
    public static object RequestBody(Guid projectId, string requestType, DateOnly? effective, DateOnly? plannedResumption = null) => new
    {
        projectId,
        requestType,
        reason = new { text = requestType == "SUSPEND" ? "Funding withheld pending the board's review." : "Funding restored by the board.", language = "en" },
        requestedEffectiveDate = effective is { } e ? Iso(e) : null,
        plannedResumptionDate = plannedResumption is { } p ? Iso(p) : null,
    };

    public static async Task<Guid> RaiseAsync(this HttpClient client, string token, object body) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, Requests, body));

    public static Task<HttpResponseMessage> CommandAsync(this HttpClient client, string token, Guid requestId, string command, string? idempotencyKey = null) =>
        client.SendAsync(HttpMethod.Post, $"{Requests}/{requestId}/{command}", token, idempotencyKey: idempotencyKey);

    public static Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, Guid requestId, string command) =>
        client.OkOrFailAsync(token, $"{Requests}/{requestId}/{command}");

    public static Task<JsonObject> RequestAsync(this HttpClient client, string token, Guid requestId) =>
        client.GetOrFailAsync(token, $"{Requests}/{requestId}");

    /// <summary>
    /// Takes a request from raising to APPROVED: raised and submitted by the entity Project Manager, its review started by the Department
    /// Manager, and approved through WF-11 by local.r02, the outcome delivered. Nothing is activated.
    /// </summary>
    public static async Task<Guid> ApprovedAsync(this SuspensionTestHost host, HttpClient client, SuspensionSessions sessions, Guid projectId, string requestType, DateOnly? effective = null)
    {
        Guid requestId = await client.RaiseAsync(sessions.EntityManager, RequestBody(projectId, requestType, effective ?? Today));
        await client.CommandOrFailAsync(sessions.EntityManager, requestId, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, requestId, "start-review");
        await host.DecideAndDeliverAsync(requestId, ApprovalTaskDecision.Approve);
        return requestId;
    }

    /// <summary>As <see cref="ApprovedAsync"/>, then activated by WF-09's own pass on its effective date, today.</summary>
    public static async Task<Guid> EffectedAsync(this SuspensionTestHost host, HttpClient client, SuspensionSessions sessions, Guid projectId, string requestType)
    {
        Guid requestId = await host.ApprovedAsync(client, sessions, projectId, requestType);
        await host.ActivateDueAsync();
        Assert.Equal("EFFECTED", (await client.RequestAsync(sessions.Officer, requestId)).Text("status"));
        return requestId;
    }

    /// <summary>
    /// One pass of WF-09's activation, as its worker runs it; returns how many requests it effected. Other tests' due requests share the
    /// database, so a test asserts its own request's status, not the count.
    /// </summary>
    public static Task<int> ActivateDueAsync(this SuspensionTestHost host) =>
        host.WithScopeAsync(services => services.GetRequiredService<ISuspensionMaintenance>().RunAsync(100, CancellationToken.None));

    /// <summary>Decides every task of the request's latest run as local.r02, then delivers the outcome, as the approver and the outbox do.</summary>
    public static async Task DecideAndDeliverAsync(this SuspensionTestHost host, Guid requestId, ApprovalTaskDecision decision)
    {
        ApprovalInstanceDetail run = (await host.RunsAsync(requestId))[^1];
        foreach (ApprovalTaskDetail task in run.Tasks.OrderBy(t => t.SequenceNo))
        {
            AdministrationResult<ApprovalInstanceDetail> decided = await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
                Person(2), task.Id, decision, new NarrativeText("Checked against the board's minute.", Language.En), CancellationToken.None));
            Assert.True(decided.Succeeded, $"Decision refused: {decided.Error}");
        }

        Guid message = Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{run.Id}-outcome'")));
        Assert.True(await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(message, CancellationToken.None));
    }

    /// <summary>The request's WF-11 runs, oldest revision first.</summary>
    public static Task<IReadOnlyList<ApprovalInstanceDetail>> RunsAsync(this SuspensionTestHost host, Guid requestId) =>
        host.WithScopeAsync(services => services.GetRequiredService<IApprovalRequests>().FindBySubjectAsync(
            SuspensionApprovalRouting.SubjectModule, SuspensionApprovalRouting.SubjectType, requestId, CancellationToken.None));

    public static async Task<T> WithScopeAsync<T>(this SuspensionTestHost host, Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>The whole row as the database holds it, as JSON text, its row version included: what "unchanged" is measured by.</summary>
    public static async Task<string> RowAsync(this SuspensionTestHost host, string table, Guid id) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT (to_jsonb(t) || jsonb_build_object('xmin', t.xmin::text))::text FROM {table} t WHERE id = '{id}'"));

    public static async Task<string> LifecycleOfAsync(this SuspensionTestHost host, Guid projectId) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT lifecycle_state FROM project.project WHERE id = '{projectId}'"));

    /// <summary>The project's suspension periods as "open" or "ended", oldest first.</summary>
    public static Task<IReadOnlyList<string>> SuspensionPeriodsAsync(this SuspensionTestHost host, Guid projectId) =>
        host.Database.QueryAsync($"SELECT CASE WHEN ended_at IS NULL THEN 'open' ELSE 'ended' END FROM suspension.active_suspension WHERE project_id = '{projectId}' ORDER BY started_at");

    /// <summary>Each of the project's baselines as "version STATUS", in version order.</summary>
    public static Task<IReadOnlyList<string>> BaselineStatesAsync(this SuspensionTestHost host, Guid projectId) =>
        host.Database.QueryAsync($"SELECT version_no || ' ' || status FROM schedule.project_baseline WHERE project_id = '{projectId}' ORDER BY version_no");

    /// <summary>The subject's audit events as "event actor_type actor", in order.</summary>
    public static Task<IReadOnlyList<string>> AuditTrailAsync(this SuspensionTestHost host, string subjectModule, Guid subjectId) =>
        host.Database.QueryAsync($"""
            SELECT event_type || ' ' || actor_type || ' ' || coalesce(actor_user_id::text, '-') FROM audit_activity.audit_event
            WHERE subject_module = '{subjectModule}' AND subject_id = '{subjectId}' ORDER BY occurred_at, id
            """);

    /// <summary>Every audit event of the project since <paramref name="since"/>, as "module event", in order.</summary>
    public static Task<IReadOnlyList<string>> ProjectEventsSinceAsync(this SuspensionTestHost host, Guid projectId, DateTimeOffset since) =>
        host.Database.QueryAsync($"""
            SELECT subject_module || ' ' || event_type FROM audit_activity.audit_event
            WHERE scope_project_id = '{projectId}' AND occurred_at > '{since.UtcDateTime:O}' ORDER BY occurred_at, id
            """);

    /// <summary>The database's clock: what a later audit event is compared with.</summary>
    public static async Task<DateTimeOffset> NowAsync(this SuspensionTestHost host) =>
        DateTimeOffset.Parse(Assert.Single(await host.Database.QueryAsync("SELECT to_char(clock_timestamp() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US\"Z\"')")), CultureInfo.InvariantCulture);

    /// <summary>The SQL error a committed statement raises — deferred checks included — or null when it succeeds.</summary>
    public static async Task<string?> RefusedAsync(this SuspensionTestHost host, string sql)
    {
        try
        {
            await host.Database.ExecuteAsync(sql);
            return null;
        }
        catch (Npgsql.PostgresException exception)
        {
            return exception.MessageText;
        }
    }

    public static async Task<JsonObject> CreatedOrFailAsync(this HttpClient client, string token, string collection, object body)
    {
        using HttpResponseMessage created = await client.PostAsync(collection, token, body);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"POST {collection}: {(int)created.StatusCode} {await created.Content.ReadAsStringAsync()}");
        return await created.ReadObjectAsync();
    }

    public static async Task<JsonObject> OkOrFailAsync(this HttpClient client, string token, string path, object? body = null)
    {
        using HttpResponseMessage response = await client.PostAsync(path, token, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"POST {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    public static async Task<JsonObject> GetOrFailAsync(this HttpClient client, string token, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path, token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    /// <summary>The status and the R-27 code of a refusal.</summary>
    public static async Task<(HttpStatusCode Status, string? Code)> RefusalAsync(this HttpResponseMessage response) =>
        (response.StatusCode, (await response.ReadObjectAsync())["code"]?.GetValue<string>());

    public static string Text(this JsonNode node, string property) => node[property]!.GetValue<string>();

    public static async Task<SuspensionSessions> SignInAsync(this HttpClient client) =>
        new(
            (await client.SignInOrFailAsync(8)).AccessToken,
            (await client.SignInOrFailAsync(2)).AccessToken,
            (await client.SignInOrFailAsync(3)).AccessToken);
}

/// <summary>
/// The people the tests act as: the entity Project Manager who raises requests and keeps the schedule (local.r08), AHDA's officer, who
/// decides and activates (local.r02), and the Department Manager, who starts reviews (local.r03).
/// </summary>
internal sealed record SuspensionSessions(string EntityManager, string Officer, string DepartmentManager);
