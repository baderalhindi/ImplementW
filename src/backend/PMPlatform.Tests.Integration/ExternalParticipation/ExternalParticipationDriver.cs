using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ExternalParticipation;

/// <summary>WF-13 and the WF-04 tasks it applies to, as their people reach them over the API, and the fixtures no WF-13 call creates.</summary>
internal static class ExternalParticipationDriver
{
    public const string Requests = "/api/v1/external-update-requests";
    public const string Contributions = "/api/v1/external-contributions";
    public const string Applications = "/api/v1/source-applications";
    public const string Tasks = "/api/v1/project-tasks";

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A new ACTIVE project of the test department, delivered by <paramref name="entity"/>, managed by local.r05.</summary>
    public static async Task<Guid> ProjectAsync(this ExternalParticipationTestHost host, Guid? entity = null, string state = "ACTIVE", string participation = "ENTITY_MANAGED")
    {
        Guid id = Guid.NewGuid();
        string formalId = state is "DRAFT" or "SUBMITTED" ? "NULL" : $"'PRJ-X{id.ToString("N")[..12]}'";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            SELECT '{id}', {formalId}, 'Participation test project', 'en', '{ExternalParticipationTestHost.ClassificationId}', '{ExternalParticipationTestHost.DepartmentId}',
                   '{entity ?? ExternalParticipationTestHost.EntityA}', '{Person(5)}', '{state}', i.id, '{participation}', {(state == "ACTIVE" ? "now()" : "NULL")},
                   now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = 'STANDARD'
            """);
        return id;
    }

    /// <summary>
    /// WF-13 EXT-CC-03: an explicit, per-project R08 assignment of the external user local.r0<paramref name="person"/> to
    /// <paramref name="projectId"/>, for their own entity — what FG-03 grants an entity's contributor on one project.
    /// </summary>
    public static Task GrantProjectAsync(this ExternalParticipationTestHost host, int person, Guid projectId) =>
        host.Database.ExecuteAsync($"""
            INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, external_entity_id, project_id, sponsor_user_id, starts_at, status,
                                                             created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), u.id, '{IdentityDatabase.ProfileVersionId(8)}', u.external_entity_id, '{projectId}', '{IdentityDatabase.UserId(2)}', now() - interval '1 hour', 'ACTIVE',
                   now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM identity_access."user" u WHERE u.id = '{Person(person)}'
            """);

    public static object RequestBody(Guid projectId, Guid entityId, Guid typeId, Guid? targetId, int? responder, int? reviewer = 5, DateOnly? due = null) => new
    {
        projectId,
        externalEntityId = entityId,
        contributionTypeItemId = typeId,
        targetId,
        instructions = new { text = "Report where the work stands, as your site team sees it.", language = "en" },
        responsibleUserId = responder is { } r ? Person(r) : (Guid?)null,
        reviewerUserId = reviewer is { } v ? Person(v) : (Guid?)null,
        dueDate = due is { } d ? Iso(d) : null,
    };

    /// <summary>A request drafted by local.r05 and issued, answering to <paramref name="responder"/> and reviewed by local.r05.</summary>
    public static async Task<Guid> IssuedRequestAsync(
        this HttpClient client, ParticipationSessions sessions, Guid projectId, Guid entityId, Guid typeId, Guid? targetId, int responder)
    {
        Guid requestId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Requests,
            RequestBody(projectId, entityId, typeId, targetId, responder, due: Today.AddDays(14))));
        await client.OkOrFailAsync(sessions.ProjectManager, $"{Requests}/{requestId}/issue");
        return requestId;
    }

    public static object[] Percent(decimal percent, string? note = null) =>
        note is null
            ? [new { fieldCode = "actualPercentComplete", value = percent.ToString(CultureInfo.InvariantCulture) }]
            : [new { fieldCode = "actualPercentComplete", value = percent.ToString(CultureInfo.InvariantCulture) }, new { fieldCode = "progressNote", value = note, language = "en" }];

    public static object[] Information(string response) => [new { fieldCode = "response", value = response, language = "en" }];

    /// <summary>The responder drafts revision 1 with <paramref name="fields"/> and submits it; the submitted revision.</summary>
    public static async Task<JsonObject> AnswerAsync(this HttpClient client, string responder, Guid requestId, object[] fields)
    {
        Guid contributionId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(responder, Contributions, new { externalUpdateRequestId = requestId, fields }));
        return await client.OkOrFailAsync(responder, $"{Contributions}/{contributionId}/submit");
    }

    /// <summary>The reviewer starts the review and decides: <c>accept</c>, <c>return</c> or <c>reject</c>.</summary>
    public static async Task<JsonObject> DecideAsync(this HttpClient client, string reviewer, Guid contributionId, string decision, string? reason = null, string? note = null)
    {
        await client.OkOrFailAsync(reviewer, $"{Contributions}/{contributionId}/start-review");
        return await client.OkOrFailAsync(reviewer, $"{Contributions}/{contributionId}/{decision}", new
        {
            reason = reason is null ? null : new { text = reason, language = "en" },
            internalNote = note is null ? null : new { text = note, language = "en" },
        });
    }

    public static Task<HttpResponseMessage> ApplyAsync(this HttpClient client, string token, Guid contributionId, Guid? key = null) =>
        client.SendAsync(HttpMethod.Post, Applications, token, new { externalContributionId = contributionId }, idempotencyKey: (key ?? Guid.NewGuid()).ToString());

    /// <summary>A task of <paramref name="projectId"/> created and started by local.r05, owned by <paramref name="owner"/>.</summary>
    public static async Task<Guid> StartedTaskAsync(this HttpClient client, ParticipationSessions sessions, Guid projectId, string title, int? owner = null)
    {
        Guid taskId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Tasks, new
        {
            projectId,
            title = new { text = title, language = "en" },
            assigneeUserId = owner is { } o ? Person(o) : (Guid?)null,
            plannedStartDate = Iso(Today),
            plannedFinishDate = Iso(Today.AddDays(9)),
        }));
        await client.OkOrFailAsync(sessions.ProjectManager, $"{Tasks}/{taskId}/start");
        return taskId;
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

    /// <summary>The ids of a collection page's items.</summary>
    public static async Task<string[]> IdsAsync(this HttpClient client, string token, string path) =>
        [.. (await client.GetOrFailAsync(token, path))["items"]!.AsArray().Select(i => i!["id"]!.GetValue<string>())];

    /// <summary>The status and the R-27 code of a refusal.</summary>
    public static async Task<(HttpStatusCode Status, string? Code)> RefusalAsync(this HttpResponseMessage response) =>
        (response.StatusCode, (await response.ReadObjectAsync())["code"]?.GetValue<string>());

    public static string Text(this JsonNode node, string property) => node[property]!.GetValue<string>();

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this ExternalParticipationTestHost host, Guid requestId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'ExternalParticipation' AND subject_id = '{requestId}' ORDER BY occurred_at, id");

    public static async Task<ParticipationSessions> SignInAsync(this HttpClient client) =>
        new(
            (await client.SignInOrFailAsync(5)).AccessToken,
            (await client.SignInOrFailAsync(3)).AccessToken,
            (await client.SignInOrFailAsync(8)).AccessToken,
            (await client.SignInOrFailAsync(7)).AccessToken,
            (await client.SignInOrFailAsync(1)).AccessToken);
}

/// <summary>
/// The people the tests act as: the internal Project Manager (local.r05), the Department Manager (local.r03), the external users of entity A
/// (local.r08) and entity B (local.r07), and the System Administrator (local.r01).
/// </summary>
internal sealed record ParticipationSessions(string ProjectManager, string DepartmentManager, string EntityA, string EntityB, string Administrator);
