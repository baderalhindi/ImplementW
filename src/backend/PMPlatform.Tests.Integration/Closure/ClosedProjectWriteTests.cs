using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Risk;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Project;

namespace PMPlatform.Tests.Integration.Closure;

/// <summary>
/// Acceptance criterion 2 and validation check 2: once a project is CLOSED, no write endpoint accepts a mutation against it. Every write
/// operation the API documents for a project's records is sent, as AHDA's officer holding every permission, at a record of a closed project
/// — and each is refused with an explicit terminal-state error, while every record of the project stays as it was to the row version.
/// </summary>
[Collection(ClosureSuite.Name)]
public sealed partial class ClosedProjectWriteTests(ClosureTestHost host)
{
    /// <summary>The modules whose write operations act on a project's records (ADR-003's core domain, WF-11 and WF-12).</summary>
    private static readonly string[] ProjectTags =
        ["Project", "Progress", "Schedule", "ProjectTask", "Milestone", "Risk", "ManagementConcern", "ChangeRequest", "Suspension", "Closure", "FinancialKpi", "DocumentManagement", "Approval"];

    /// <summary>Writes of those modules that address no record of a project, and why.</summary>
    private static readonly Dictionary<string, string> NotOfAProject = new(StringComparer.Ordinal)
    {
        ["POST /api/v1/projects"] = "registers a new project",
        ["POST /api/v1/approval-delegations"] = "a person's delegation, of no project",
        ["POST /api/v1/approval-delegations/{delegationId}/revoke"] = "a person's delegation, of no project",
        ["POST /api/v1/change-requests/{changeRequestId}/preview-materiality"] = "a read sent as POST (CHANGE_REQUEST_VIEW): it computes a preview and writes nothing",
    };

    /// <summary>String values a request's validation knows but the document does not enumerate, by property name.</summary>
    private static readonly Dictionary<string, JsonNode?> Known = new(StringComparer.Ordinal)
    {
        ["participationMode"] = "ENTITY_MANAGED",
        ["dependencyType"] = "FS",
        ["actionType"] = "MITIGATE",
        ["concernType"] = "ISSUE",
        ["plannedResumptionDate"] = null,
    };

    /// <summary>
    /// The explicit terminal-state errors: PROJECT_CLOSED from every module for a write to a closed project's records, DOCUMENT_PROJECT_CLOSED
    /// from WF-12, and TERMINAL_STATE from the project itself and from WF-11 for the closed project's decided run.
    /// </summary>
    private static readonly HashSet<string> TerminalCodes = new(StringComparer.Ordinal) { "PROJECT_CLOSED", "DOCUMENT_PROJECT_CLOSED", "TERMINAL_STATE" };

    [Fact]
    public async Task AClosedProjectTakesNoWriteThroughAnyEndpoint()
    {
        using HttpClient client = host.Api.CreateClient();
        string officer = (await client.SignInOrFailAsync(2)).AccessToken;
        ClosedProjectFixture closed = await ClosedProjectFixture.CreateAsync(host.Database, ClosureTestHost.DocumentTypeId, ClosureTestHost.InternalClassificationId);
        OpenApiDocument document = await OpenApiDocument.FetchAsync(client);
        string before = await closed.DigestAsync(host.Database);
        DateTimeOffset since = DateTimeOffset.UtcNow;

        List<string> sent = [];
        List<string> accepted = [];
        foreach ((string name, string path, JsonObject operation) in Writes(document))
        {
            string target = Targets().Replace(path, m => closed[RecordOf(path, m.Groups[1].Value)].ToString());
            using HttpResponseMessage response = await SendAsync(client, officer, name.Split(' ')[0], target, Body(document, operation, closed));
            sent.Add(name);
            JsonObject? problem = response.Content.Headers.ContentLength is 0 ? null : JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonObject;
            string? code = problem?["code"]?.GetValue<string>();
            if (response.StatusCode != HttpStatusCode.Conflict || code is null || !TerminalCodes.Contains(code))
            {
                accepted.Add($"{name}: {(int)response.StatusCode} {code} {problem?["errors"]?.ToJsonString()}");
            }
        }

        Assert.True(accepted.Count == 0, $"A write to a closed project was not refused as terminal:\n  {string.Join("\n  ", accepted)}");
        Assert.Equal(before, await closed.DigestAsync(host.Database));
        Assert.Empty(await host.Database.QueryAsync($"""
            SELECT event_type FROM audit_activity.audit_event
            WHERE scope_project_id = '{closed.ProjectId}' AND outcome = 'SUCCESS' AND event_class IN ('DATA_CHANGE', 'LIFECYCLE_TRANSITION') AND occurred_at >= '{since.UtcDateTime:O}'
            """));

        // The sweep cannot pass on nothing: every module's writes were sent, these among them.
        Assert.Equal(ProjectTags.Order(), ProjectTags.Where(tag => document.Operations(tag).Any(o => sent.Contains(o.Name))).Order());
        Assert.Contains("PUT /api/v1/projects/{projectId}", sent);
        Assert.Contains("POST /api/v1/project-tasks/{taskId}/complete", sent);
        Assert.Contains("POST /api/v1/post-project-obligations/{obligationId}/satisfy", sent);
        Assert.Contains("POST /api/v1/documents", sent);

        // WF-12's and FG-03's own rules agree: no document goes to a closed project, and no one is given access to it.
        using HttpResponseMessage assignment = await client.PostAsync(AdministrationApi.AccessRelationships, (await client.SignInOrFailAsync(1)).AccessToken, new
        {
            userId = IdentityDatabase.UserId(6),
            permissionProfileVersionId = IdentityDatabase.ProfileVersionId(6),
            projectId = closed.ProjectId,
        });
        Assert.Equal(
            (HttpStatusCode.UnprocessableEntity, "IDENTITY_ACCESS_PROJECT_CLOSED"),
            (assignment.StatusCode, (await assignment.ReadObjectAsync())["code"]?.GetValue<string>()));
    }

    /// <summary>
    /// A closed project's records take no write from a background pass either: WF-06's acceptance expiry leaves a closed project's accepted
    /// risks as they are — and steps past them, so that however many there are, they never keep another project's lapsed acceptance from
    /// expiring in a batch.
    /// </summary>
    [Fact]
    public async Task TheRiskExpiryPassLeavesAClosedProjectAsItIsAndReachesTheOthers()
    {
        Guid closed = await host.ProjectAsync(state: "CLOSED");
        Guid active = await host.ProjectAsync();
        Guid[] closedRisks = [Guid.NewGuid(), Guid.NewGuid()];
        Guid activeRisk = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            BEGIN;
            SET LOCAL session_replication_role = replica;
            {AcceptedRisk(closedRisks[0], closed, lapsedDaysAgo: 3)}
            {AcceptedRisk(closedRisks[1], closed, lapsedDaysAgo: 2)}
            {AcceptedRisk(activeRisk, active, lapsedDaysAgo: 1)}
            COMMIT;
            """);
        string closedBefore = string.Join('|', await Task.WhenAll(closedRisks.Select(r => host.RowAsync("risk.risk", r))));

        await host.WithScopeAsync(services => services.GetRequiredService<IRiskMaintenance>().RunAsync(1, CancellationToken.None));

        Assert.Equal(closedBefore, string.Join('|', await Task.WhenAll(closedRisks.Select(r => host.RowAsync("risk.risk", r)))));
        Assert.Equal(["ACTIVE", "ACTIVE"], await host.Database.QueryAsync($"SELECT status FROM risk.risk_acceptance WHERE risk_id IN ('{closedRisks[0]}', '{closedRisks[1]}')"));
        Assert.Equal(["ASSESSED EXPIRED"], await host.Database.QueryAsync(
            $"SELECT r.status || ' ' || a.status FROM risk.risk r JOIN risk.risk_acceptance a ON a.risk_id = r.id WHERE r.id = '{activeRisk}'"));
    }

    /// <summary>The write operations of <see cref="ProjectTags"/> that address a project's records, as (METHOD path, path, operation).</summary>
    private static IEnumerable<(string Name, string Path, JsonObject Operation)> Writes(OpenApiDocument document) =>
        from tag in ProjectTags
        from operation in document.Operations(tag)
        where operation.Name.Split(' ')[0] is "POST" or "PUT" or "PATCH" or "DELETE" && !NotOfAProject.ContainsKey(operation.Name)
        select (operation.Name, operation.Path, operation.Operation);

    /// <summary>A MONITORING risk of the project with an ACTIVE acceptance that lapsed <paramref name="lapsedDaysAgo"/> days ago.</summary>
    private static string AcceptedRisk(Guid riskId, Guid projectId, int lapsedDaysAgo) => $"""
        INSERT INTO risk.risk (id, project_id, risk_category_item_id, status, identified_date, description, description_lang, title, title_lang, created_at, created_by, updated_at, updated_by)
        VALUES ('{riskId}', '{projectId}', gen_random_uuid(), 'MONITORING', current_date - 30, 'Residual.', 'en', 'Residual', 'en', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
        INSERT INTO risk.risk_acceptance (id, risk_id, accepted_by_user_id, accepted_at, expires_on, rationale, rationale_lang, status, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{riskId}', '{IdentityDatabase.UserId(2)}', now() - interval '20 days', current_date - {lapsedDaysAgo}, 'Tolerated.', 'en', 'ACTIVE', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}');
        """;

    /// <summary>The fixture record a path parameter names, read from the collection it follows.</summary>
    private static string RecordOf(string path, string parameter) => (CollectionOf(path, parameter), parameter) switch
    {
        ("approval-tasks", "taskId") => "approvalTask",
        ("approval-instances", "instanceId") => "approvalInstance",
        ("task-dependencies", "dependencyId") => "taskDependency",
        ("schedule-dependencies", "dependencyId") => "scheduleDependency",
        ("completion-cases", "caseId") => "completionCase",
        ("closure-cases", "caseId") => "closureCase",
        ("project-tasks", "taskId") => "task",
        ("project-milestones", "milestoneId") => "milestone",
        ("schedule-activities", "activityId") => "activity",
        ("project-baselines", "baselineId") => "baseline",
        ("risk-treatment-actions", "actionId") => "action",
        ("concern-escalations", "escalationId") => "escalation",
        ("management-concerns", "concernId") => "concern",
        ("financial-progress-updates", "updateId") => "update",
        ("kpi-assignments", "assignmentId") => "assignment",
        ("kpi-target-versions", "targetVersionId") => "target",
        ("kpi-measurements", "measurementId") => "measurement",
        ("financial-source-modes", "sourceModeId") => "sourceMode",
        ("financial-commitments", "commitmentId") => "commitment",
        ("suspension-requests", "suspensionRequestId") => "suspensionRequest",
        ("progress-submissions", "submissionId") => "submission",
        ("milestone-achievements", "achievementId") => "achievement",
        ("post-project-obligations", "obligationId") => "obligation",
        ("change-requests", "changeRequestId") => "changeRequest",
        ("documents", "documentId") => "document",
        ("versions", "versionId") => "version",
        ("risks", "riskId") => "risk",
        ("projects", "projectId") => "project",
        (_, "evidenceReferenceId") => "evidenceReference",
        var unknown => throw new InvalidOperationException($"No fixture record for {unknown} in {path}: add one."),
    };

    private static string CollectionOf(string path, string parameter)
    {
        string[] segments = path.Split('/');
        return segments[Array.IndexOf(segments, $"{{{parameter}}}") - 1];
    }

    /// <summary>
    /// A body every property of the request schema filled with a well-formed value — a reference to the closed project's matching record
    /// where the property names one — so that what answers is the service, not the request's validation.
    /// </summary>
    private static HttpContent? Body(OpenApiDocument document, JsonObject operation, ClosedProjectFixture closed)
    {
        if (operation["requestBody"]?["content"]?["application/json"]?["schema"] is { } schema)
        {
            return JsonContent(Sample(document, schema, "body", closed, 0));
        }

        // WF-12's uploads read the multipart form themselves and document no body.
        if (operation["operationId"]?.GetValue<string>() is "DocumentManagement_CreateDocument" or "DocumentManagement_AddDocumentVersion")
        {
            ByteArrayContent file = new("%PDF-1.4\n%closed\n"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return new MultipartFormDataContent
            {
                { file, "file", "closed.pdf" },
                { new StringContent("Closed project check"), "title.text" },
                { new StringContent("en"), "title.language" },
                { new StringContent(ClosureTestHost.DocumentTypeId.ToString()), "documentTypeItemId" },
                { new StringContent(ClosureTestHost.InternalClassificationId.ToString()), "dataClassificationItemId" },
                { new StringContent(closed.ProjectId.ToString()), "projectId" },
            };
        }

        return null;
    }

    private static JsonNode? Sample(OpenApiDocument document, JsonNode schemaNode, string property, ClosedProjectFixture closed, int depth)
    {
        Schema schema = document.Resolve(schemaNode) ?? throw new InvalidOperationException($"No schema for {property}.");
        if (depth > 8)
        {
            return null;
        }

        if (schema.Properties.Count > 0)
        {
            JsonObject value = [];
            foreach ((string name, JsonNode? child) in schema.Properties)
            {
                value[name] = Sample(document, child!, name, closed, depth + 1);
            }

            return value;
        }

        return schema.Types.Contains("array") ? new JsonArray(Sample(document, schema.Node["items"]!, property, closed, depth + 1)) : Scalar(schema, property, closed);
    }

    private static JsonNode? Scalar(Schema schema, string property, ClosedProjectFixture closed) => schema switch
    {
        _ when Known.TryGetValue(property, out JsonNode? known) => known?.DeepClone(),
        { Enum.Count: > 0 } => JsonNode.Parse(schema.Enum[0]),
        _ when schema.Node["pattern"]?.GetValue<string>() == MoneyPattern => JsonValue.Create("1.00"),
        _ when schema.Types.Contains("integer") || schema.Types.Contains("number") => JsonValue.Create(1),
        _ when schema.Types.Contains("boolean") => JsonValue.Create(false),
        { Format: "uuid" } => Reference(property, closed) is { } id ? JsonValue.Create(id.ToString()) : null,
        { Format: "date" } => JsonValue.Create(DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        { Format: "date-time" } => JsonValue.Create(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
        _ => JsonValue.Create(property == "language" ? "en" : "Closed project check"),
    };

    /// <summary>The closed project's record a referencing property names; any other id is a fresh one, or none where it must be omitted.</summary>
    private static Guid? Reference(string property, ClosedProjectFixture closed) => property switch
    {
        "projectId" => closed.ProjectId,
        "riskId" => closed["risk"],
        "managementConcernId" => closed["concern"],
        "kpiAssignmentId" => closed["assignment"],
        "kpiTargetVersionId" => closed["target"],
        "projectMilestoneId" => closed["milestone"],
        "scheduleActivityId" or "predecessorActivityId" => closed["activity"],
        "successorActivityId" => closed["activity2"],
        "predecessorTaskId" => closed["task"],
        "successorTaskId" => closed["task2"],
        "reportingCycleId" => closed["cycle"],
        "completionCaseId" => closed["completionCase"],
        "documentId" => closed["document"],
        "documentVersionId" or "versionId" => closed["version"],
        "closureCaseId" or "parentTaskId" or "changeAuthorizationId" => null,
        _ => Guid.NewGuid(),
    };

    /// <summary>A SAR amount, as the API takes it: a string with two decimals (ADR-008).</summary>
    private const string MoneyPattern = @"^-?[0-9]{1,16}\.[0-9]{2}$";

    private static StringContent JsonContent(JsonNode? body) => new(body?.ToJsonString() ?? "{}", System.Text.Encoding.UTF8, "application/json");

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, string method, string path, HttpContent? body)
    {
        HttpRequestMessage request = new(new HttpMethod(method), path) { Content = body };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        return client.SendAsync(request);
    }

    [GeneratedRegex(@"\{([A-Za-z]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Targets();
}
