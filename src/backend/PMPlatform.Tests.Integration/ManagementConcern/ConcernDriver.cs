using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;
using PMPlatform.Tests.Integration.Risk;

namespace PMPlatform.Tests.Integration.ManagementConcern;

/// <summary>WF-07 as the SPA reaches it, FG-04's scale publication and WF-11's decisions as their people make them, and the fixtures no WF-07 call creates.</summary>
internal static class ConcernDriver
{
    public const string Concerns = "/api/v1/management-concerns";
    public const string Escalations = "/api/v1/concern-escalations";
    public const string EscalatedEvent = "ManagementConcern.ConcernEscalated";

    /// <summary>The severity each overall level maps to in the scale the host publishes: 1–2 MINOR, 3–4 MAJOR, 5 CRITICAL.</summary>
    public static readonly IReadOnlyDictionary<short, string> DefaultMapping = new Dictionary<short, string>
    {
        [1] = "TEST_MINOR",
        [2] = "TEST_MINOR",
        [3] = "TEST_MAJOR",
        [4] = "TEST_MAJOR",
        [5] = "TEST_CRITICAL",
    };

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A new project in <paramref name="state"/>, of the test department and the active entity, managed by <paramref name="projectManager"/>.</summary>
    public static async Task<Guid> ProjectAsync(this ConcernTestHost host, string state = "ACTIVE", int projectManager = 5, string profile = "STANDARD", bool ofEntity = true)
    {
        Guid id = Guid.NewGuid();
        string activatedAt = state == "ACTIVE" ? "now()" : "NULL";
        string formalId = state is "DRAFT" or "SUBMITTED" ? "NULL" : $"'PRJ-C{id.ToString("N")[..12]}'";
        string entity = ofEntity ? $"'{ConcernTestHost.EntityId}'" : "NULL";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            SELECT '{id}', {formalId}, 'Concern test project', 'en', '{ConcernTestHost.ClassificationId}', '{ConcernTestHost.DepartmentId}',
                   {entity}, '{Person(projectManager)}', '{state}', i.id, '{(ofEntity ? "ENTITY_MANAGED" : "AHDA_MANAGED")}', {activatedAt},
                   now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = '{profile}'
            """);
        return id;
    }

    /// <summary>
    /// Publishes a RISK_MATRIX version as FG-04's author, reviewer and publisher do: the seeded scale (OQ-006), complete enough to rate
    /// a risk, with the values naming each overall level's CONCERN_SEVERITY code as <paramref name="mapping"/> says. Effective now.
    /// </summary>
    public static async Task<Guid> PublishScaleAsync(this ConcernTestHost host, HttpClient client, Crew crew, IReadOnlyDictionary<short, string> mapping)
    {
        Guid seeded = Guid.Parse(Assert.Single(await host.Database.QueryAsync("SELECT md5('configuration_version:RISK_MATRIX:1')")));
        (Guid version, _) = await client.CreateVersionAsync(crew, "RISK_MATRIX", basedOnVersionId: seeded);
        using HttpResponseMessage read = await client.GetAsync($"{ConfigurationApi.Versions}/{version}", crew.Author);
        JsonObject content = (await read.ReadObjectAsync())["content"]!.AsObject();
        content["riskRatings"] = new JsonArray(
            new JsonObject { ["code"] = "LOW", ["label"] = new JsonObject { ["ar"] = "منخفض", ["en"] = "Low" }, ["sortOrder"] = 1 },
            new JsonObject { ["code"] = "HIGH", ["label"] = new JsonObject { ["ar"] = "مرتفع", ["en"] = "High" }, ["sortOrder"] = 2 });
        content["riskMatrixCells"] = new JsonArray([
            .. Enumerable.Range(1, 5).SelectMany(p => Enumerable.Range(1, 5).Select(i =>
                (JsonNode)new JsonObject { ["probabilityLevel"] = p, ["impactLevel"] = i, ["ratingCode"] = p * i >= 12 ? "HIGH" : "LOW" })),
        ]);
        content["values"] = new JsonArray([
            .. mapping.Select(m => (JsonNode)new JsonObject { ["key"] = $"CONCERN_SEVERITY_LEVEL_{m.Key}", ["type"] = "TEXT", ["value"] = m.Value }),
        ]);
        await client.WriteAndPublishAsync(crew, version, AdministrationApi.ETagOf(read), content.DeepClone());
        return version;
    }

    /// <summary>The four IMPACT_DIMENSION items db/seed publishes (ADR-011), in display order.</summary>
    public static async Task<Guid[]> DimensionsAsync(this ConcernTestHost host) =>
        [.. (await host.Database.QueryAsync("""
            SELECT i.id::text FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'IMPACT_DIMENSION' AND i.lifecycle_state = 'PUBLISHED' ORDER BY i.sort_order
            """)).Select(Guid.Parse)];

    /// <summary>Impacts on the first dimensions, at <paramref name="levels"/> in order.</summary>
    public static object[] Impacts(Guid[] dimensions, params short[] levels) =>
        [.. levels.Select((level, i) => new { impactDimensionItemId = dimensions[i], impactLevel = level })];

    public static object ConcernBody(Guid projectId, object[]? impacts = null, string type = "ISSUE", Guid? category = null, Guid? priority = null, DateOnly? target = null) => new
    {
        projectId,
        concernType = type,
        title = new { text = "Site access blocked by utility works", language = "en" },
        description = new { text = "The utility contractor closed the only access road to the site.", language = "en" },
        categoryItemId = category ?? ConcernTestHost.CategoryId,
        priorityItemId = priority ?? ConcernTestHost.HighPriorityId,
        impacts,
        targetResolutionDate = target is { } t ? Iso(t) : null,
    };

    public static async Task<Guid> RaiseAsync(this HttpClient client, string token, Guid projectId, object[]? impacts = null) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, Concerns, ConcernBody(projectId, impacts)));

    public static Task<HttpResponseMessage> CommandAsync(this HttpClient client, string token, Guid concernId, string command, object? body = null) =>
        client.PostAsync($"{Concerns}/{concernId}/{command}", token, body);

    public static Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, Guid concernId, string command, object? body = null) =>
        client.OkOrFailAsync(token, $"{Concerns}/{concernId}/{command}", body);

    public static Task<JsonObject> ConcernAsync(this HttpClient client, string token, Guid concernId) =>
        client.GetOrFailAsync(token, $"{Concerns}/{concernId}");

    public static object Resolution(string text) => new { resolution = new { text, language = "en" } };

    /// <summary>Takes a concern from OPEN to IN_PROGRESS as AHDA's officer, assigned to the Department Manager.</summary>
    public static async Task StartAsync(this HttpClient client, ConcernSessions sessions, Guid concernId)
    {
        await client.CommandOrFailAsync(sessions.Officer, concernId, "assign", new { assigneeUserId = Person(3) });
        await client.CommandOrFailAsync(sessions.Officer, concernId, "start");
    }

    /// <summary>Escalates with an explicit <c>Idempotency-Key</c>, as a client retrying the same request does.</summary>
    public static Task<HttpResponseMessage> EscalateAsync(this HttpClient client, string token, Guid concernId, Guid key, string reason = "Blocked for two weeks; needs the utility owner.") =>
        client.SendAsync(HttpMethod.Post, Escalations, token, new { managementConcernId = concernId, reason = new { text = reason, language = "en" } }, idempotencyKey: key.ToString());

    /// <summary>
    /// The validator (local.r02, R02) decides the WF-11 task of the concern's current run — <c>approve</c> or <c>return</c> — and the
    /// outbox delivers the outcome to WF-07.
    /// </summary>
    public static async Task DecideAsync(this ConcernTestHost host, HttpClient client, ConcernSessions sessions, Guid concernId, string decision)
    {
        JsonArray inbox = (await client.GetOrFailAsync(sessions.Officer, "/api/v1/approval-tasks?pageSize=100"))["items"]!.AsArray();
        string taskId = inbox.Single(t => t!["instance"]!["subject"]!["id"]!.GetValue<string>() == concernId.ToString())!["taskId"]!.GetValue<string>();
        await client.OkOrFailAsync(sessions.Officer, $"/api/v1/approval-tasks/{taskId}/{decision}", new { reason = new { text = "Checked on site.", language = "en" } });
        await host.DispatchAsync();
    }

    /// <summary>The outbox dispatcher's pass: every due message to its consumer.</summary>
    public static Task<int> DispatchAsync(this ConcernTestHost host) =>
        host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchDueAsync(500, CancellationToken.None);

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this ConcernTestHost host, Guid concernId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'ManagementConcern' AND subject_id = '{concernId}' ORDER BY occurred_at, id");

    public static async Task<ConcernSessions> SignInAsync(this HttpClient client) =>
        new(
            (await client.SignInOrFailAsync(8)).AccessToken,
            (await client.SignInOrFailAsync(5)).AccessToken,
            (await client.SignInOrFailAsync(2)).AccessToken,
            (await client.SignInOrFailAsync(3)).AccessToken);

    public static async Task AssertStatusAsync(this HttpResponseMessage response, HttpStatusCode status, string? code = null) =>
        Assert.Equal((status, code), await response.RefusalAsync());
}

/// <summary>
/// The people most tests act as: the entity Project Manager (local.r08), an internal Project Manager (local.r05), AHDA's issue officer
/// and validator (local.r02), and the Department Manager escalations are routed to (local.r03).
/// </summary>
internal sealed record ConcernSessions(string EntityManager, string InternalManager, string Officer, string DepartmentManager);
