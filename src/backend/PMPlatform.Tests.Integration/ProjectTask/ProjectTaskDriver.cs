using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>WF-04 as the SPA reaches it, and the fixtures no WF-04 call creates: projects and their schedule activities.</summary>
internal static class ProjectTaskDriver
{
    public const string Tasks = "/api/v1/project-tasks";
    public const string Dependencies = "/api/v1/task-dependencies";
    public const string ActivityProgress = "/api/v1/activity-execution-progresses";

    /// <summary>The first planned day of every test task.</summary>
    public static readonly DateOnly Day1 = new(2027, 1, 3);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A new project in <paramref name="state"/>, of the test department and the active entity, managed by <paramref name="projectManager"/>.</summary>
    public static async Task<Guid> ProjectAsync(this ProjectTaskTestHost host, string state = "ACTIVE", int projectManager = 8)
    {
        Guid id = Guid.NewGuid();
        string activatedAt = state == "ACTIVE" ? "now()" : "NULL";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            SELECT '{id}', 'PRJ-T{id.ToString("N")[..12]}', 'Task test project', 'en', '{ProjectTaskTestHost.ClassificationId}', '{ProjectTaskTestHost.DepartmentId}',
                   '{ProjectTaskTestHost.EntityId}', '{Person(projectManager)}', '{state}', i.id, 'ENTITY_MANAGED', {activatedAt},
                   now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD'
            """);
        return id;
    }

    /// <summary>The project's schedule with one leaf activity per WBS code, PLANNED, as WF-03 leaves them. Returns the activities.</summary>
    public static async Task<Guid[]> ActivitiesAsync(this ProjectTaskTestHost host, Guid projectId, params string[] wbsCodes)
    {
        Guid schedule = Guid.NewGuid();
        Guid[] activities = [.. wbsCodes.Select(_ => Guid.NewGuid())];
        string rows = string.Join(",\n", wbsCodes.Select((wbs, i) => $"""
            ('{activities[i]}', '{schedule}', '{wbs}', 'Activity {wbs}', 'en', 'ACTIVITY', current_date, current_date, current_date + 9, 10, current_date, current_date + 9,
             'PLANNED', {i}, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """));
        await host.Database.ExecuteAsync($"""
            INSERT INTO schedule.project_schedule (id, project_id, created_at, created_by, updated_at, updated_by)
            VALUES ('{schedule}', '{projectId}', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
            INSERT INTO schedule.schedule_activity (id, project_schedule_id, wbs_code, name, name_lang, activity_kind, requested_start_date, planned_start_date,
                                                    planned_finish_date, planned_duration_days, forecast_start_date, forecast_finish_date, status, sort_order,
                                                    created_at, created_by, updated_at, updated_by)
            VALUES {rows};
            """);
        return activities;
    }

    /// <summary>A task planned over <paramref name="days"/> days from <see cref="Day1"/>.</summary>
    public static object TaskBody(Guid projectId, string title, int days, Guid? activityId = null, Guid? parentTaskId = null, Guid? assigneeUserId = null) => new
    {
        projectId,
        parentTaskId,
        scheduleActivityId = activityId,
        title = new { text = title, language = "en" },
        assigneeUserId,
        plannedStartDate = Iso(Day1),
        plannedFinishDate = Iso(Day1.AddDays(days - 1)),
    };

    public static async Task<Guid> TaskAsync(
        this HttpClient client, string token, Guid projectId, string title, int days, Guid? activityId = null, Guid? parentTaskId = null, Guid? assigneeUserId = null) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, Tasks, TaskBody(projectId, title, days, activityId, parentTaskId, assigneeUserId)));

    /// <summary>Posts to a collection and returns the new resource, asserting 201.</summary>
    public static async Task<JsonObject> CreatedOrFailAsync(this HttpClient client, string token, string collection, object body)
    {
        using HttpResponseMessage created = await client.PostAsync(collection, token, body);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"POST {collection}: {(int)created.StatusCode} {await created.Content.ReadAsStringAsync()}");
        return await created.ReadObjectAsync();
    }

    /// <summary>Sends a task command, e.g. <c>start</c>, and returns the response for the test to read.</summary>
    public static Task<HttpResponseMessage> CommandAsync(this HttpClient client, string token, Guid taskId, string command, object? body = null) =>
        client.PostAsync($"{Tasks}/{taskId}/{command}", token, body);

    /// <summary>Sends a task command and returns the task, asserting 200.</summary>
    public static async Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, Guid taskId, string command, object? body = null)
    {
        using HttpResponseMessage response = await client.CommandAsync(token, taskId, command, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    public static Task<JsonObject> BlockOrFailAsync(this HttpClient client, string token, Guid taskId) =>
        client.CommandOrFailAsync(token, taskId, "block", new { reason = new { text = "Waiting for the site permit", language = "en" } });

    public static Task<JsonObject> ReportOrFailAsync(this HttpClient client, string token, Guid taskId, decimal percent) =>
        client.CommandOrFailAsync(token, taskId, "report-progress", new { actualPercentComplete = percent });

    public static Task<HttpResponseMessage> LinkAsync(this HttpClient client, string token, Guid predecessor, Guid successor, string type = "FS") =>
        client.PostAsync(Dependencies, token, new { predecessorTaskId = predecessor, successorTaskId = successor, dependencyType = type });

    public static async Task<Guid> LinkOrFailAsync(this HttpClient client, string token, Guid predecessor, Guid successor, string type = "FS")
    {
        using HttpResponseMessage linked = await client.LinkAsync(token, predecessor, successor, type);
        Assert.True(linked.StatusCode == HttpStatusCode.Created, $"link: {(int)linked.StatusCode} {await linked.Content.ReadAsStringAsync()}");
        return AdministrationApi.IdOf(await linked.ReadObjectAsync());
    }

    public static async Task<JsonObject> GetTaskAsync(this HttpClient client, string token, Guid taskId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{Tasks}/{taskId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadObjectAsync();
    }

    /// <summary>The project's items of a WF-04 collection, first page.</summary>
    public static async Task<JsonArray> ItemsAsync(this HttpClient client, string token, string collection, Guid projectId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{collection}?projectId={projectId}&pageSize=200", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadObjectAsync())["items"]!.AsArray();
    }

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this ProjectTaskTestHost host, Guid subjectId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'ProjectTask' AND subject_id = '{subjectId}' ORDER BY occurred_at, id");

    public static Task<IReadOnlyList<string>> StatusInDatabaseAsync(this ProjectTaskTestHost host, Guid taskId) =>
        host.Database.QueryAsync($"SELECT status FROM project_task.project_task WHERE id = '{taskId}'");

    /// <summary>The status and the R-27 code of a refusal.</summary>
    public static async Task<(HttpStatusCode Status, string? Code)> RefusalAsync(this HttpResponseMessage response) =>
        (response.StatusCode, (await response.ReadObjectAsync())["code"]?.GetValue<string>());

    public static string Text(this JsonNode node, string property) => node[property]!.GetValue<string>();

    public static decimal Percent(this JsonNode node, string property) => node[property]!.GetValue<decimal>();

    public static async Task<TaskSessions> SignInAsync(this HttpClient client) =>
        new((await client.SignInOrFailAsync(8)).AccessToken, (await client.SignInOrFailAsync(6)).AccessToken, (await client.SignInOrFailAsync(2)).AccessToken);
}

/// <summary>
/// The people most tests act as: the entity Project Manager (local.r08), a task owner (local.r06), and an editor of every task
/// who holds no reopen permission (local.r02).
/// </summary>
internal sealed record TaskSessions(string ProjectManager, string Owner, string Editor);
