using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.MasterDataConfig;

namespace PMPlatform.Tests.Integration.Risk;

/// <summary>WF-06 as the SPA reaches it, FG-04's matrix publication as its administrators do it, and the fixtures no WF-06 call creates.</summary>
internal static class RiskDriver
{
    public const string Risks = "/api/v1/risks";
    public const string Assessments = "/api/v1/risk-assessments";
    public const string Acceptances = "/api/v1/risk-acceptances";
    public const string Actions = "/api/v1/risk-treatment-actions";

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// A new project in <paramref name="state"/>, of the test department and the active entity, managed by
    /// <paramref name="projectManager"/>, on the governance profile <paramref name="profile"/>.
    /// </summary>
    public static async Task<Guid> ProjectAsync(this RiskTestHost host, string state = "ACTIVE", int projectManager = 8, string profile = "STANDARD")
    {
        Guid id = Guid.NewGuid();
        string activatedAt = state == "ACTIVE" ? "now()" : "NULL";
        string formalId = state is "DRAFT" or "SUBMITTED" ? "NULL" : $"'PRJ-R{id.ToString("N")[..12]}'";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            SELECT '{id}', {formalId}, 'Risk test project', 'en', '{RiskTestHost.ClassificationId}', '{RiskTestHost.DepartmentId}',
                   '{RiskTestHost.EntityId}', '{Person(projectManager)}', '{state}', i.id, 'ENTITY_MANAGED', {activatedAt},
                   now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = '{profile}'
            """);
        return id;
    }

    /// <summary>
    /// Publishes a RISK_MATRIX version as FG-04's author, reviewer and publisher do: a copy of the seeded scale with the ratings LOW
    /// and HIGH, HIGH where probability × impact reaches <paramref name="highFrom"/>, labelled in English "Low" and "High" unless
    /// <paramref name="englishLabels"/> says otherwise. It takes effect now. Returns its id.
    /// </summary>
    public static async Task<Guid> PublishMatrixAsync(this RiskTestHost host, HttpClient client, Crew crew, int highFrom, (string Low, string High)? englishLabels = null)
    {
        (string low, string high) = englishLabels ?? ("Low", "High");
        // db/seed's DRAFT version 1 of RISK_MATRIX, the generic scale (OQ-006).
        Guid seeded = Guid.Parse(Assert.Single(await host.Database.QueryAsync("SELECT md5('configuration_version:RISK_MATRIX:1')")));
        (Guid version, _) = await client.CreateVersionAsync(crew, "RISK_MATRIX", basedOnVersionId: seeded);
        using HttpResponseMessage read = await client.GetAsync($"{ConfigurationApi.Versions}/{version}", crew.Author);
        JsonObject content = (await read.ReadObjectAsync())["content"]!.AsObject();
        content["riskRatings"] = new JsonArray(
            new JsonObject { ["code"] = "LOW", ["label"] = new JsonObject { ["ar"] = "منخفض", ["en"] = low }, ["sortOrder"] = 1 },
            new JsonObject { ["code"] = "HIGH", ["label"] = new JsonObject { ["ar"] = "مرتفع", ["en"] = high }, ["sortOrder"] = 2 });
        content["riskMatrixCells"] = new JsonArray([
            .. Enumerable.Range(1, 5).SelectMany(p => Enumerable.Range(1, 5).Select(i =>
                (JsonNode)new JsonObject { ["probabilityLevel"] = p, ["impactLevel"] = i, ["ratingCode"] = p * i >= highFrom ? "HIGH" : "LOW" })),
        ]);
        await client.WriteAndPublishAsync(crew, version, AdministrationApi.ETagOf(read), content.DeepClone());
        return version;
    }

    /// <summary>The four IMPACT_DIMENSION items db/seed publishes (ADR-011), in display order.</summary>
    public static async Task<Guid[]> DimensionsAsync(this RiskTestHost host) =>
        [.. (await host.Database.QueryAsync("""
            SELECT i.id::text FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'IMPACT_DIMENSION' AND i.lifecycle_state = 'PUBLISHED' ORDER BY i.sort_order
            """)).Select(Guid.Parse)];

    public static object RiskBody(Guid projectId, string title = "Contractor mobilisation delay", Guid? category = null, Guid? ownerUserId = null, DateOnly? identified = null) => new
    {
        projectId,
        title = new { text = title, language = "en" },
        description = new { text = "The contractor may not mobilise by the planned start.", language = "en" },
        riskCategoryItemId = category ?? RiskTestHost.CategoryId,
        ownerUserId,
        identifiedDate = Iso(identified ?? Today),
    };

    public static async Task<Guid> RiskAsync(this HttpClient client, string token, Guid projectId) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, Risks, RiskBody(projectId)));

    /// <summary>An assessment at <paramref name="probability"/>, every dimension at <paramref name="impact"/> but the first at <paramref name="peak"/>.</summary>
    public static object Assessment(Guid[] dimensions, short probability, short impact, short? peak = null) => new
    {
        probabilityLevel = probability,
        impacts = dimensions.Select((d, i) => new { impactDimensionItemId = d, impactLevel = i == 0 && peak is { } p ? p : impact }).ToArray(),
        rationale = new { text = "Assessed at the monthly risk review.", language = "en" },
    };

    public static object Rationale(string text) => new { rationale = new { text, language = "en" } };

    /// <summary>Posts to a collection and returns the new resource, asserting 201.</summary>
    public static async Task<JsonObject> CreatedOrFailAsync(this HttpClient client, string token, string collection, object body)
    {
        using HttpResponseMessage created = await client.PostAsync(collection, token, body);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"POST {collection}: {(int)created.StatusCode} {await created.Content.ReadAsStringAsync()}");
        return await created.ReadObjectAsync();
    }

    /// <summary>Sends a risk command, e.g. <c>close</c>, and returns the response for the test to read.</summary>
    public static Task<HttpResponseMessage> CommandAsync(this HttpClient client, string token, Guid riskId, string command, object? body = null) =>
        client.PostAsync($"{Risks}/{riskId}/{command}", token, body);

    /// <summary>Sends a command to <paramref name="path"/> and returns the resource, asserting 200.</summary>
    public static async Task<JsonObject> OkOrFailAsync(this HttpClient client, string token, string path, object? body = null)
    {
        using HttpResponseMessage response = await client.PostAsync(path, token, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"POST {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    public static Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, Guid riskId, string command, object? body = null) =>
        client.OkOrFailAsync(token, $"{Risks}/{riskId}/{command}", body);

    public static async Task<JsonObject> GetOrFailAsync(this HttpClient client, string token, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path, token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    /// <summary>The risk's ETag as the API answers it now.</summary>
    public static async Task<string> ETagAsync(this HttpClient client, string token, Guid riskId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{Risks}/{riskId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return AdministrationApi.ETagOf(response);
    }

    /// <summary>The status and the R-27 code of a refusal.</summary>
    public static async Task<(HttpStatusCode Status, string? Code)> RefusalAsync(this HttpResponseMessage response) =>
        (response.StatusCode, response.Content.Headers.ContentLength == 0 ? null : (await response.ReadObjectAsync())["code"]?.GetValue<string>());

    public static string Text(this JsonNode node, string property) => node[property]!.GetValue<string>();

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this RiskTestHost host, Guid subjectId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'Risk' AND subject_id = '{subjectId}' ORDER BY occurred_at, id");

    public static async Task<RiskSessions> SignInAsync(this HttpClient client) =>
        new(
            (await client.SignInOrFailAsync(8)).AccessToken,
            (await client.SignInOrFailAsync(5)).AccessToken,
            (await client.SignInOrFailAsync(2)).AccessToken,
            (await client.SignInOrFailAsync(3)).AccessToken);
}

/// <summary>
/// The people most tests act as: the entity Project Manager (local.r08), an internal Project Manager (local.r05), AHDA's risk officer
/// who edits every risk but holds no reopen (local.r02), and the holder of the reopen (local.r03).
/// </summary>
internal sealed record RiskSessions(string EntityManager, string InternalManager, string Officer, string Reopener);
