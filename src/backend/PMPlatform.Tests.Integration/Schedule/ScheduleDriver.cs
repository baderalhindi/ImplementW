using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>WF-03 as the SPA reaches it, WF-11's side of a baseline review driven in process, and the fixtures no API creates.</summary>
internal static class ScheduleDriver
{
    public const string Schedules = "/api/v1/project-schedules";
    public const string Activities = "/api/v1/schedule-activities";
    public const string Dependencies = "/api/v1/schedule-dependencies";
    public const string Baselines = "/api/v1/project-baselines";
    public const string HealthStatuses = "/api/v1/schedule-health-statuses";

    /// <summary>The first day of every test plan.</summary>
    public static readonly DateOnly Day1 = new(2027, 1, 3);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// A new project in <paramref name="state"/>, of the test department and the active entity, managed by
    /// <paramref name="projectManager"/>, under <paramref name="profileId"/> (STANDARD when null).
    /// </summary>
    public static async Task<Guid> ProjectAsync(
        this ScheduleTestHost host, string state = "APPROVED_PLANNED", int projectManager = 8, Guid? profileId = null, DateOnly? legacyIntakeDate = null)
    {
        Guid id = Guid.NewGuid();
        string activatedAt = state == "ACTIVE" ? "now()" : "NULL";
        string formal = state is "DRAFT" or "SUBMITTED" or "UNDER_REVIEW" or "RETURNED" ? "NULL" : $"'PRJ-S{id.ToString("N")[..12]}'";
        string intake = legacyIntakeDate is { } date ? $"'{Iso(date)}'" : "NULL";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, legacy_intake_date,
                                         created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', {formal}, 'Schedule test project', 'en', '{ScheduleTestHost.ClassificationId}', '{ScheduleTestHost.DepartmentId}', '{ScheduleTestHost.EntityId}',
                    '{Person(projectManager)}', '{state}', '{profileId ?? host.StandardProfileId}', 'ENTITY_MANAGED', {activatedAt}, {intake},
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """);
        return id;
    }

    /// <summary>A project with an initialized schedule.</summary>
    public static async Task<Guid> ScheduledProjectAsync(this ScheduleTestHost host, HttpClient client, string token, string state = "APPROVED_PLANNED", Guid? profileId = null)
    {
        Guid projectId = await host.ProjectAsync(state, profileId: profileId);
        await client.CreatedOrFailAsync(token, Schedules, new { projectId });
        return projectId;
    }

    /// <summary>Posts to a collection and returns the new resource, asserting 201.</summary>
    public static async Task<JsonObject> CreatedOrFailAsync(this HttpClient client, string token, string collection, object body)
    {
        using HttpResponseMessage created = await client.PostAsync(collection, token, body);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"POST {collection}: {(int)created.StatusCode} {await created.Content.ReadAsStringAsync()}");
        return await created.ReadObjectAsync();
    }

    /// <summary>An activity of <paramref name="days"/> working days requested from <see cref="Day1"/> + <paramref name="startOffset"/>.</summary>
    public static object Activity(Guid projectId, string wbs, int startOffset, int days, Guid? parent = null) => new
    {
        projectId,
        parentActivityId = parent,
        wbsCode = wbs,
        name = new { text = $"Activity {wbs}", language = "en" },
        requestedStartDate = Iso(Day1.AddDays(startOffset)),
        plannedDurationDays = days,
    };

    public static async Task<Guid> ActivityAsync(this HttpClient client, string token, Guid projectId, string wbs, int startOffset, int days, Guid? parent = null) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, Activities, Activity(projectId, wbs, startOffset, days, parent)));

    public static Task<HttpResponseMessage> LinkAsync(this HttpClient client, string token, Guid predecessor, Guid successor, string type = "FS", int lag = 0) =>
        client.PostAsync(Dependencies, token, new { predecessorActivityId = predecessor, successorActivityId = successor, dependencyType = type, lagDays = lag });

    public static async Task<Guid> LinkOrFailAsync(this HttpClient client, string token, Guid predecessor, Guid successor, string type = "FS", int lag = 0)
    {
        using HttpResponseMessage linked = await client.LinkAsync(token, predecessor, successor, type, lag);
        Assert.True(linked.StatusCode == HttpStatusCode.Created, $"link: {(int)linked.StatusCode} {await linked.Content.ReadAsStringAsync()}");
        return AdministrationApi.IdOf(await linked.ReadObjectAsync());
    }

    /// <summary>Sends a command and returns the resource, asserting 200.</summary>
    public static async Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, string path, object? body = null)
    {
        using HttpResponseMessage response = await client.PostAsync(path, token, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"POST {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    /// <summary>Opens a DRAFT candidate of the project and submits it; returns it as submitted.</summary>
    public static async Task<JsonObject> SubmittedBaselineAsync(this HttpClient client, string token, Guid projectId, Guid? changeAuthorizationId = null)
    {
        Guid baselineId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, Baselines, new { projectId }));
        return await client.CommandOrFailAsync(token, $"{Baselines}/{baselineId}/submit", new { changeAuthorizationId });
    }

    /// <summary>A schedule of one 10-day activity, baselined, approved by local.r02 and delivered. Returns the activity and the baseline.</summary>
    public static async Task<(Guid Activity, Guid Baseline)> ApprovedBaselineAsync(this ScheduleTestHost host, HttpClient client, string token, Guid projectId)
    {
        Guid activity = await client.ActivityAsync(token, projectId, "1", 0, 10);
        Guid baseline = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(token, projectId));
        await host.DecideAndDeliverAsync(baseline, ApprovalTaskDecision.Approve);
        return (activity, baseline);
    }

    /// <summary>The baseline's review runs, oldest revision first.</summary>
    public static Task<IReadOnlyList<ApprovalInstanceDetail>> RunsAsync(this ScheduleTestHost host, Guid baselineId) =>
        host.WithScopeAsync(services => services.GetRequiredService<IApprovalRequests>()
            .FindBySubjectAsync(ScheduleApprovalRouting.SubjectModule, ScheduleApprovalRouting.SubjectType, baselineId, CancellationToken.None));

    /// <summary>Decides the open task of the baseline's latest run as local.r02 and delivers the outcome: what the approver and the outbox do.</summary>
    public static async Task DecideAndDeliverAsync(this ScheduleTestHost host, Guid baselineId, ApprovalTaskDecision decision, string? reason = null)
    {
        ApprovalInstanceDetail run = (await host.RunsAsync(baselineId))[^1];
        AdministrationResult<ApprovalInstanceDetail> decided = await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
            Person(2), run.Tasks.Single(t => t.Status == Domain.Approval.ApprovalTaskStatus.Pending).Id, decision,
            reason is null ? null : new NarrativeText(reason, Language.En), CancellationToken.None));
        Assert.True(decided.Succeeded, $"Decision refused: {decided.Error}");
        Assert.True(await host.DeliverAsync(run.Id));
    }

    /// <summary>Dispatches the run's outcome message now; false when the consumer threw and the message waits for a retry.</summary>
    public static async Task<bool> DeliverAsync(this ScheduleTestHost host, Guid runId)
    {
        Guid message = Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{runId}-outcome'")));
        return await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(message, CancellationToken.None);
    }

    public static async Task<T> WithScopeAsync<T>(this ScheduleTestHost host, Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>The project's activities as the API lists them, keyed by WBS code.</summary>
    public static async Task<Dictionary<string, JsonObject>> ActivitiesAsync(this HttpClient client, string token, Guid projectId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{Activities}?projectId={projectId}&pageSize=200", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadObjectAsync())["items"]!.AsArray().Select(i => i!.AsObject()).ToDictionary(i => i["wbsCode"]!.GetValue<string>());
    }

    /// <summary>The project's items of a WF-03 collection, first page.</summary>
    public static async Task<JsonArray> ItemsAsync(this HttpClient client, string token, string collection, Guid projectId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{collection}?projectId={projectId}&pageSize=200", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadObjectAsync())["items"]!.AsArray();
    }

    /// <summary>Each of the project's baselines as "version STATUS", in version order, as the database holds them.</summary>
    public static Task<IReadOnlyList<string>> BaselineStatesAsync(this ScheduleTestHost host, Guid projectId) =>
        host.Database.QueryAsync($"SELECT version_no || ' ' || status FROM schedule.project_baseline WHERE project_id = '{projectId}' ORDER BY version_no");

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this ScheduleTestHost host, Guid subjectId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'Schedule' AND subject_id = '{subjectId}' ORDER BY occurred_at, id");

    /// <summary>The status and the R-27 code of a refusal.</summary>
    public static async Task<(HttpStatusCode Status, string? Code)> RefusalAsync(this HttpResponseMessage response) =>
        (response.StatusCode, (await response.ReadObjectAsync())["code"]?.GetValue<string>());

    public static string Text(this JsonNode node, string property) => node[property]!.GetValue<string>();

    public static int? Days(this JsonNode node, string property) => node[property]?.GetValue<int>();

    public static async Task<ScheduleSessions> SignInAsync(this HttpClient client) =>
        new((await client.SignInOrFailAsync(8)).AccessToken, (await client.SignInOrFailAsync(2)).AccessToken);
}

/// <summary>The people most tests act as: the entity Project Manager (local.r08) and local.r02, who decides and activates.</summary>
internal sealed record ScheduleSessions(string ProjectManager, string Portfolio);
