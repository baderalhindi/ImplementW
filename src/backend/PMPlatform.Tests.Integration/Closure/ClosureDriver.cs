using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Closure;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Closure;

/// <summary>
/// WF-10 as the SPA reaches it; WF-03 and WF-04 as the Project Manager keeps the schedule and the tasks; WF-11's decisions as local.r02
/// makes them, delivered by the outbox when a test says; WF-10's activation pass when a test runs it; and the fixtures no API creates.
/// </summary>
internal static class ClosureDriver
{
    public const string CompletionCases = "/api/v1/completion-cases";
    public const string ClosureCases = "/api/v1/closure-cases";
    public const string Obligations = "/api/v1/post-project-obligations";
    public const string ReadinessChecks = "/api/v1/readiness-checks";
    public const string Projects = "/api/v1/projects";
    public const string Tasks = "/api/v1/project-tasks";
    public const string Schedules = "/api/v1/project-schedules";
    public const string Activities = "/api/v1/schedule-activities";
    public const string Baselines = "/api/v1/project-baselines";
    public const string SuspensionRequests = "/api/v1/suspension-requests";

    /// <summary>The API's today: the UTC date.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static object Narrative(string text) => new { text, language = "en" };

    /// <summary>
    /// A new project of the test department and the active entity, managed by local.r08, in <paramref name="state"/> — activated ten days
    /// ago — under <paramref name="profile"/>.
    /// </summary>
    public static async Task<Guid> ProjectAsync(this ClosureTestHost host, string state = "ACTIVE", string profile = "LIGHT")
    {
        Guid id = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            SELECT '{id}', 'PRJ-C{id.ToString("N")[..12]}', 'Closeout test project', 'en', '{ClosureTestHost.ClassificationId}', '{ClosureTestHost.DepartmentId}',
                   '{ClosureTestHost.EntityId}', '{Person(8)}', '{state}', i.id, 'ENTITY_MANAGED', now() - interval '10 days',
                   now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = '{profile}'
            """);
        return id;
    }

    /// <summary>
    /// WF-03 on a LIGHT project: a schedule of one activity of 20 working days from today, baselined by its Project Manager at once
    /// (ADR-015). Returns the activity.
    /// </summary>
    public static async Task<Guid> BaselinedActivityAsync(this HttpClient client, ClosureSessions sessions, Guid projectId)
    {
        await client.CreatedOrFailAsync(sessions.EntityManager, Schedules, new { projectId });
        Guid activityId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, Activities, new
        {
            projectId,
            wbsCode = "1",
            name = Narrative("Works"),
            requestedStartDate = Iso(Today),
            plannedDurationDays = 20,
        }));
        Guid candidate = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, Baselines, new { projectId }));
        Assert.Equal("ACTIVE", (await client.OkOrFailAsync(sessions.EntityManager, $"{Baselines}/{candidate}/submit", new { changeAuthorizationId = (Guid?)null })).Text("status"));
        return activityId;
    }

    /// <summary>A task of the activity, planned over the next ten days, assigned to its Project Manager.</summary>
    public static async Task<Guid> TaskAsync(this HttpClient client, ClosureSessions sessions, Guid projectId, Guid activityId) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, Tasks, new
        {
            projectId,
            scheduleActivityId = activityId,
            title = Narrative("Install the works"),
            assigneeUserId = Person(8),
            plannedStartDate = Iso(Today),
            plannedFinishDate = Iso(Today.AddDays(10)),
        }));

    /// <summary>A completion case's body: today as the actual completion date, and the Project Manager's narrative.</summary>
    public static object CompletionBody(Guid projectId, DateOnly? completedOn = null) => new
    {
        projectId,
        actualProjectCompletionDate = Iso(completedOn ?? Today),
        completionNarrative = Narrative("Works handed over to the operator."),
    };

    public static object ClosureBody(Guid projectId, string summary = "Closeout complete; records archived.") => new { projectId, closureNarrative = Narrative(summary) };

    public static async Task<Guid> RaiseAsync(this HttpClient client, string token, string collection, object body) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, collection, body));

    public static Task<HttpResponseMessage> CommandAsync(this HttpClient client, string token, string collection, Guid id, string command, object? body = null) =>
        client.PostAsync($"{collection}/{id}/{command}", token, body);

    public static Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, string collection, Guid id, string command, object? body = null) =>
        client.OkOrFailAsync(token, $"{collection}/{id}/{command}", body);

    /// <summary>Waives every failed, waivable criterion of the case's latest evaluation, as the Department Manager accepting the exceptions.</summary>
    public static async Task WaiveFailedAsync(this HttpClient client, ClosureSessions sessions, string collection, Guid caseId)
    {
        JsonObject evaluated = await client.CommandOrFailAsync(sessions.EntityManager, collection, caseId, "evaluate-readiness");
        foreach (JsonNode? check in evaluated["readiness"]!["checks"]!.AsArray().Where(c => c!.Text("result") == "FAIL" && c!["waivable"]!.GetValue<bool>()))
        {
            await client.CommandOrFailAsync(sessions.DepartmentManager, collection, caseId, "waive-check",
                new { checkCode = check!.Text("checkCode"), reason = Narrative("Accepted by the Department Manager.") });
        }
    }

    /// <summary>
    /// Takes a case from raised to APPROVED: raised by the entity Project Manager, its failed criteria waived by the Department Manager,
    /// submitted, its review started by the Department Manager and approved through WF-11 by local.r02, the outcome delivered. Not activated.
    /// </summary>
    public static async Task<Guid> ApprovedAsync(this ClosureTestHost host, HttpClient client, ClosureSessions sessions, string collection, object body)
    {
        Guid caseId = await client.RaiseAsync(sessions.EntityManager, collection, body);
        await client.WaiveFailedAsync(sessions, collection, caseId);
        await client.CommandOrFailAsync(sessions.EntityManager, collection, caseId, "submit");
        await client.CommandOrFailAsync(sessions.DepartmentManager, collection, caseId, "start-review");
        await host.DecideAndDeliverAsync(SubjectTypeOf(collection), caseId, ApprovalTaskDecision.Approve);
        return caseId;
    }

    /// <summary>As <see cref="ApprovedAsync"/>, then activated by WF-10's own pass.</summary>
    public static async Task<Guid> EffectedAsync(this ClosureTestHost host, HttpClient client, ClosureSessions sessions, string collection, object body)
    {
        Guid caseId = await host.ApprovedAsync(client, sessions, collection, body);
        await host.ActivateDueAsync();
        Assert.Equal("EFFECTED", (await client.GetOrFailAsync(sessions.Officer, $"{collection}/{caseId}")).Text("status"));
        return caseId;
    }

    public static string SubjectTypeOf(string collection) => collection == CompletionCases ? "CompletionCase" : "ClosureCase";

    /// <summary>
    /// One pass of WF-10's activation, as its worker runs it; returns how many cases it effected. Other tests' approved cases share the
    /// database, so a test asserts its own case's status, not the count.
    /// </summary>
    public static Task<int> ActivateDueAsync(this ClosureTestHost host) =>
        host.WithScopeAsync(services => services.GetRequiredService<ICloseoutMaintenance>().RunAsync(100, CancellationToken.None));

    /// <summary>Decides every task of the subject's latest run as local.r02, then delivers the outcome, as the approver and the outbox do.</summary>
    public static async Task DecideAndDeliverAsync(this ClosureTestHost host, string subjectType, Guid subjectId, ApprovalTaskDecision decision, string subjectModule = "Closure")
    {
        ApprovalInstanceDetail run = (await host.RunsAsync(subjectModule, subjectType, subjectId))[^1];
        foreach (ApprovalTaskDetail task in run.Tasks.OrderBy(t => t.SequenceNo))
        {
            AdministrationResult<ApprovalInstanceDetail> decided = await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
                Person(2), task.Id, decision, new NarrativeText("Checked against the closeout file.", Language.En), CancellationToken.None));
            Assert.True(decided.Succeeded, $"Decision refused: {decided.Error}");
        }

        Assert.True(await host.DeliverAsync(run.Id));
    }

    /// <summary>Delivers the run's outcome, as the outbox worker would.</summary>
    public static async Task<bool> DeliverAsync(this ClosureTestHost host, Guid runId)
    {
        Guid message = Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{runId}-outcome'")));
        return await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(message, CancellationToken.None);
    }

    /// <summary>The subject's WF-11 runs, oldest revision first.</summary>
    public static Task<IReadOnlyList<ApprovalInstanceDetail>> RunsAsync(this ClosureTestHost host, string subjectModule, string subjectType, Guid subjectId) =>
        host.WithScopeAsync(services => services.GetRequiredService<IApprovalRequests>().FindBySubjectAsync(subjectModule, subjectType, subjectId, CancellationToken.None));

    public static async Task<T> WithScopeAsync<T>(this ClosureTestHost host, Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>The whole row as the database holds it, as JSON text, its row version included: what "unchanged" is measured by.</summary>
    public static async Task<string> RowAsync(this ClosureTestHost host, string table, Guid id) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT (to_jsonb(t) || jsonb_build_object('xmin', t.xmin::text))::text FROM {table} t WHERE id = '{id}'"));

    public static async Task<string> LifecycleOfAsync(this ClosureTestHost host, Guid projectId) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT lifecycle_state FROM project.project WHERE id = '{projectId}'"));

    /// <summary>The subject's audit events as "event actor_type actor", in order.</summary>
    public static Task<IReadOnlyList<string>> AuditTrailAsync(this ClosureTestHost host, string subjectModule, Guid subjectId) =>
        host.Database.QueryAsync($"""
            SELECT event_type || ' ' || actor_type || ' ' || coalesce(actor_user_id::text, '-') FROM audit_activity.audit_event
            WHERE subject_module = '{subjectModule}' AND subject_id = '{subjectId}' ORDER BY occurred_at, id
            """);

    /// <summary>The SQL error a committed statement raises — deferred checks included — or null when it succeeds.</summary>
    public static async Task<string?> RefusedAsync(this ClosureTestHost host, string sql)
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

    /// <summary>The readiness criteria of a case representation as "CODE RESULT", in order.</summary>
    public static IReadOnlyList<string> Checks(this JsonObject @case) =>
        [.. @case["readiness"]!["checks"]!.AsArray().Select(c => $"{c!.Text("checkCode")} {c!.Text("result")}")];

    public static async Task<ClosureSessions> SignInAsync(this HttpClient client) =>
        new(
            (await client.SignInOrFailAsync(8)).AccessToken,
            (await client.SignInOrFailAsync(2)).AccessToken,
            (await client.SignInOrFailAsync(3)).AccessToken);
}

/// <summary>
/// The people the tests act as: the entity Project Manager who raises cases and keeps tasks and the schedule (local.r08), AHDA's officer,
/// who decides and activates (local.r02), and the Department Manager, who starts reviews and waives criteria (local.r03).
/// </summary>
internal sealed record ClosureSessions(string EntityManager, string Officer, string DepartmentManager);
