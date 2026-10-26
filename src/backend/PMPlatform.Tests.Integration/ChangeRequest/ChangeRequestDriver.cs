using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ChangeRequest;

/// <summary>
/// WF-08 as the SPA reaches it; WF-03 and WF-14 as their people make the changes WF-08 authorises; WF-11's decisions as their deciders
/// make them, delivered by the outbox when a test says; and the fixtures no API creates.
/// </summary>
internal static class ChangeRequestDriver
{
    public const string Requests = "/api/v1/change-requests";
    public const string Authorizations = "/api/v1/change-authorizations";
    public const string Schedules = "/api/v1/project-schedules";
    public const string Activities = "/api/v1/schedule-activities";
    public const string Baselines = "/api/v1/project-baselines";
    public const string Commitments = "/api/v1/financial-commitments";

    /// <summary>R03, the Department Manager's role, as db/seed numbers it.</summary>
    public static readonly Guid DepartmentManagerRoleId = new("00000000-0000-4000-8000-000000000003");

    /// <summary>The first day of every test plan.</summary>
    public static readonly DateOnly Day1 = new(2027, 1, 3);

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A new project in <paramref name="state"/>, of the test department and the active entity, managed by <paramref name="projectManager"/>.</summary>
    public static async Task<Guid> ProjectAsync(this ChangeRequestTestHost host, string profile = "STANDARD", int projectManager = 8, string state = "ACTIVE")
    {
        Guid id = Guid.NewGuid();
        string activatedAt = state == "ACTIVE" ? "now()" : "NULL";
        string formalId = state is "DRAFT" or "SUBMITTED" or "UNDER_REVIEW" or "RETURNED" ? "NULL" : $"'PRJ-W{id.ToString("N")[..12]}'";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            SELECT '{id}', {formalId}, 'Change test project', 'en', '{ChangeRequestTestHost.ClassificationId}', '{ChangeRequestTestHost.DepartmentId}',
                   '{ChangeRequestTestHost.EntityId}', '{Person(projectManager)}', '{state}', i.id, 'ENTITY_MANAGED', {activatedAt},
                   now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}'
            FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
            WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = '{profile}'
            """);
        return id;
    }

    /// <summary>
    /// WF-03: a schedule of one activity of <paramref name="days"/> working days from <see cref="Day1"/>, baselined by its Project Manager —
    /// at once on a LIGHT project, through WF-11 (local.r02 approves) otherwise. Returns the ACTIVE Approved Baseline.
    /// </summary>
    public static async Task<Guid> BaselinedAsync(this ChangeRequestTestHost host, HttpClient client, ChangeSessions sessions, Guid projectId, int days = 20)
    {
        await client.CreatedOrFailAsync(sessions.EntityManager, Schedules, new { projectId });
        await client.CreatedOrFailAsync(sessions.EntityManager, Activities, new
        {
            projectId,
            wbsCode = "1",
            name = new { text = "Works", language = "en" },
            requestedStartDate = Iso(Day1),
            plannedDurationDays = days,
        });
        JsonObject baseline = await client.SubmittedBaselineAsync(sessions.EntityManager, projectId, null);
        Guid id = AdministrationApi.IdOf(baseline);
        if (baseline.Text("status") == "SUBMITTED")
        {
            await host.DecideAndDeliverAsync("Schedule", "ProjectBaseline", id, ApprovalTaskDecision.Approve);
        }

        return id;
    }

    /// <summary>WF-03: opens a candidate of the project's working schedule and submits it, naming <paramref name="changeAuthorizationId"/>.</summary>
    public static async Task<JsonObject> SubmittedBaselineAsync(this HttpClient client, string token, Guid projectId, Guid? changeAuthorizationId)
    {
        using HttpResponseMessage submitted = await client.SubmitBaselineAsync(token, projectId, changeAuthorizationId);
        Assert.True(submitted.StatusCode == HttpStatusCode.OK, $"baseline submit: {(int)submitted.StatusCode} {await submitted.Content.ReadAsStringAsync()}");
        return await submitted.ReadObjectAsync();
    }

    /// <summary>WF-03: opens a candidate and sends its submission, with an explicit <c>Idempotency-Key</c> when given.</summary>
    public static async Task<HttpResponseMessage> SubmitBaselineAsync(this HttpClient client, string token, Guid projectId, Guid? changeAuthorizationId, string? idempotencyKey = null)
    {
        Guid candidate = AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, Baselines, new { projectId }));
        return await client.SendAsync(HttpMethod.Post, $"{Baselines}/{candidate}/submit", token, new { changeAuthorizationId }, idempotencyKey: idempotencyKey);
    }

    /// <summary>
    /// WF-14: an Approved Budget version of <paramref name="amountSar"/>, entered by the Project Manager with its referenced document,
    /// approved by local.r02 and ACTIVE. A change to an ACTIVE budget names <paramref name="changeAuthorizationId"/>.
    /// </summary>
    public static async Task<Guid> BudgetedAsync(
        this ChangeRequestTestHost host, HttpClient client, ChangeSessions sessions, Guid projectId, string amountSar, Guid? changeAuthorizationId = null)
    {
        Guid id = await host.SubmittedBudgetAsync(client, sessions, projectId, amountSar, changeAuthorizationId);
        await host.DecideAndDeliverAsync("FinancialKpi", "FinancialCommitment", id, ApprovalTaskDecision.Approve);
        return id;
    }

    /// <summary>WF-14: an Approved Budget version, entered with its referenced document and submitted to WF-11.</summary>
    public static async Task<Guid> SubmittedBudgetAsync(
        this ChangeRequestTestHost host, HttpClient client, ChangeSessions sessions, Guid projectId, string amountSar, Guid? changeAuthorizationId = null)
    {
        Guid id = await host.DraftBudgetAsync(client, sessions, projectId, amountSar);
        await client.OkOrFailAsync(sessions.EntityManager, $"{Commitments}/{id}/submit", new { changeAuthorizationId });
        return id;
    }

    /// <summary>WF-14: an Approved Budget version, DRAFT, entered by the Project Manager with its referenced document held CLEAN.</summary>
    public static async Task<Guid> DraftBudgetAsync(this ChangeRequestTestHost host, HttpClient client, ChangeSessions sessions, Guid projectId, string amountSar)
    {
        Guid id = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.EntityManager, Commitments,
            new { projectId, amountSar, sourceReference = "Board minute 7/2027", asOfDate = Iso(Today) }));
        using HttpResponseMessage uploaded = await client.UploadAsync(
            sessions.EntityManager, DocumentDriver.Text($"Budget approval {Guid.NewGuid()}"), projectId, ChangeRequestTestHost.Internal,
            documentType: ChangeRequestTestHost.DocumentTypeId);
        Assert.True(uploaded.StatusCode == HttpStatusCode.Created, $"upload: {(int)uploaded.StatusCode} {await uploaded.Content.ReadAsStringAsync()}");
        JsonObject document = await uploaded.ReadObjectAsync();
        await host.Api.ScanAsync();
        await client.CreatedOrFailAsync(sessions.EntityManager, $"{Commitments}/{id}/documents", new
        {
            documentId = AdministrationApi.IdOf(document),
            documentVersionId = document["latestVersion"]!.Text("id"),
            evidenceTypeItemId = ChangeRequestTestHost.EvidenceTypeId,
        });
        return id;
    }

    /// <summary>
    /// Publishes a MATERIALITY_BAND version effective now, as the host's but with band 2's cost threshold at
    /// <paramref name="costBand2Percent"/>, and returns its id: the version later evaluations resolve and pin.
    /// </summary>
    public static async Task<Guid> PublishBandsAsync(this ChangeRequestTestHost host, decimal costBand2Percent)
    {
        Guid version = Guid.NewGuid();
        string seed = IdentityDatabase.SeedPrincipalId;
        string percent = costBand2Percent.ToString(CultureInfo.InvariantCulture);
        await host.Database.ExecuteAsync($"""
            INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, created_at, created_by, updated_at, updated_by)
            SELECT '{version}', f.id, (SELECT max(v.version_no) + 1 FROM master_data_config.configuration_version v WHERE v.configuration_family_id = f.id),
                   'DRAFT', now(), '{seed}', now(), '{seed}'
            FROM master_data_config.configuration_family f WHERE f.code = 'MATERIALITY_BAND';

            INSERT INTO master_data_config.materiality_band (id, configuration_version_id, governance_profile_item_id, band_no, cost_threshold_pct, cost_threshold_sar,
                                                             schedule_threshold_pct, schedule_threshold_days, scope_rule_code, requires_approval,
                                                             created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), '{version}', b.governance_profile_item_id, b.band_no, CASE b.band_no WHEN 2 THEN {percent} ELSE b.cost_threshold_pct END,
                   b.cost_threshold_sar, b.schedule_threshold_pct, b.schedule_threshold_days, b.scope_rule_code, b.requires_approval, now(), '{seed}', now(), '{seed}'
            FROM master_data_config.materiality_band b WHERE b.configuration_version_id = '{ChangeRequestTestHost.MaterialityVersionId}';

            UPDATE master_data_config.configuration_version
            SET lifecycle_state = 'PUBLISHED', published_at = now(), effective_from = now() - interval '1 second'
            WHERE id = '{version}';
            """);
        return version;
    }

    /// <summary>A change request's body: the type and the impacts stated; the rest a plain justification.</summary>
    public static object RequestBody(
        Guid projectId, string changeType, string? costImpactSar = null, int? scheduleImpactDays = null, string? scopeImpact = null, bool contractual = false,
        Guid? profileItemId = null) => new
        {
            projectId,
            changeType,
            title = new { text = $"Change of {changeType.ToLowerInvariant()}", language = "en" },
            justification = new { text = "The ground survey found rock where the design assumed sand.", language = "en" },
            costImpactSar,
            scheduleImpactDays,
            scopeImpact = scopeImpact is null ? null : new { text = scopeImpact, language = "en" },
            isContractualObligation = contractual,
            requestedGovernanceProfileItemId = profileItemId,
        };

    public static async Task<Guid> RaiseAsync(this HttpClient client, string token, object body) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, Requests, body));

    public static Task<HttpResponseMessage> CommandAsync(this HttpClient client, string token, Guid requestId, string command) =>
        client.PostAsync($"{Requests}/{requestId}/{command}", token);

    public static Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, Guid requestId, string command) =>
        client.OkOrFailAsync(token, $"{Requests}/{requestId}/{command}");

    public static Task<JsonObject> RequestAsync(this HttpClient client, string token, Guid requestId) =>
        client.GetOrFailAsync(token, $"{Requests}/{requestId}");

    /// <summary>The requester submits; AHDA's officer starts the review, which records the materiality and starts the WF-11 run.</summary>
    public static async Task<JsonObject> UnderReviewAsync(this HttpClient client, ChangeSessions sessions, Guid requestId, string? requester = null)
    {
        await client.CommandOrFailAsync(requester ?? sessions.EntityManager, requestId, "submit");
        return await client.CommandOrFailAsync(sessions.Officer, requestId, "start-review");
    }

    /// <summary>
    /// Takes a request from DRAFT to APPROVED: submitted, under review, and approved by every stage of its run — local.r02, and local.r03
    /// for band 3 — with the outcome delivered.
    /// </summary>
    public static async Task<JsonObject> ApprovedAsync(this ChangeRequestTestHost host, HttpClient client, ChangeSessions sessions, Guid requestId, string? requester = null)
    {
        await client.UnderReviewAsync(sessions, requestId, requester);
        await host.DecideAndDeliverAsync(ChangeRequestApprovalRouting.SubjectModule, ChangeRequestApprovalRouting.SubjectType, requestId, ApprovalTaskDecision.Approve);
        return await client.RequestAsync(sessions.Officer, requestId);
    }

    /// <summary>As <see cref="ApprovedAsync"/>, then AHDA opens its implementation. Returns its authorisations, in issue order.</summary>
    public static async Task<JsonArray> ImplementingAsync(this ChangeRequestTestHost host, HttpClient client, ChangeSessions sessions, Guid requestId)
    {
        await host.ApprovedAsync(client, sessions, requestId);
        return (await client.CommandOrFailAsync(sessions.Officer, requestId, "start-implementation"))["authorizations"]!.AsArray();
    }

    /// <summary>The id of the authorisation of <paramref name="scope"/> among a request's.</summary>
    public static Guid AuthorizationOf(JsonArray authorizations, string scope) =>
        AdministrationApi.IdOf(authorizations.Single(a => a!.Text("authorizationScope") == scope)!.AsObject());

    /// <summary>
    /// Decides every stage of the subject's latest run in turn — local.r02 for its tasks, local.r03 for the Department Manager's — then
    /// delivers the outcome, as the approvers and the outbox do.
    /// </summary>
    public static async Task DecideAndDeliverAsync(this ChangeRequestTestHost host, string subjectModule, string subjectType, Guid subjectId, ApprovalTaskDecision decision) =>
        Assert.True(await host.DeliverAsync((await host.DecideAsync(subjectModule, subjectType, subjectId, decision)).Id));

    /// <summary>As <see cref="DecideAndDeliverAsync"/> without the delivery: the outcome waits in the outbox. Returns the run decided.</summary>
    public static async Task<ApprovalInstanceDetail> DecideAsync(this ChangeRequestTestHost host, string subjectModule, string subjectType, Guid subjectId, ApprovalTaskDecision decision)
    {
        ApprovalInstanceDetail run = (await host.RunsAsync(subjectModule, subjectType, subjectId))[^1];
        foreach (short stage in run.Tasks.Select(t => t.SequenceNo).Distinct().Order())
        {
            foreach (ApprovalTaskDetail task in run.Tasks.Where(t => t.SequenceNo == stage))
            {
                int decider = task.AssignedRoleId == DepartmentManagerRoleId ? 3 : 2;
                AdministrationResult<ApprovalInstanceDetail> decided = await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
                    Person(decider), task.Id, decision, new NarrativeText("Checked against the survey.", Language.En), CancellationToken.None));
                Assert.True(decided.Succeeded, $"Decision refused: {decided.Error}");
            }

            if (decision != ApprovalTaskDecision.Approve)
            {
                break;
            }
        }

        return run;
    }

    /// <summary>Dispatches the run's outcome message now; false when the consumer threw and the message waits for a retry.</summary>
    public static async Task<bool> DeliverAsync(this ChangeRequestTestHost host, Guid runId) =>
        await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(await host.OutcomeMessageAsync(runId), CancellationToken.None);

    /// <summary>The outbox message that carries the run's outcome to its subject's module.</summary>
    public static async Task<Guid> OutcomeMessageAsync(this ChangeRequestTestHost host, Guid runId) =>
        Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{runId}-outcome'")));

    /// <summary>The subject's WF-11 runs, oldest revision first.</summary>
    public static Task<IReadOnlyList<ApprovalInstanceDetail>> RunsAsync(this ChangeRequestTestHost host, string subjectModule, string subjectType, Guid subjectId) =>
        host.WithScopeAsync(services => services.GetRequiredService<IApprovalRequests>().FindBySubjectAsync(subjectModule, subjectType, subjectId, CancellationToken.None));

    public static async Task<T> WithScopeAsync<T>(this ChangeRequestTestHost host, Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>The whole row as the database holds it, as JSON text, its row version included: what "unchanged" is measured by.</summary>
    public static async Task<string> RowAsync(this ChangeRequestTestHost host, string table, Guid id) =>
        Assert.Single(await host.Database.QueryAsync($"SELECT (to_jsonb(t) || jsonb_build_object('xmin', t.xmin::text))::text FROM {table} t WHERE id = '{id}'"));

    /// <summary>Each of the project's baselines as "version STATUS", in version order.</summary>
    public static Task<IReadOnlyList<string>> BaselineStatesAsync(this ChangeRequestTestHost host, Guid projectId) =>
        host.Database.QueryAsync($"SELECT version_no || ' ' || status FROM schedule.project_baseline WHERE project_id = '{projectId}' ORDER BY version_no");

    /// <summary>Each of the project's Approved Budget versions as "version STATUS amount", in version order.</summary>
    public static Task<IReadOnlyList<string>> BudgetStatesAsync(this ChangeRequestTestHost host, Guid projectId) =>
        host.Database.QueryAsync($"""
            SELECT version_no || ' ' || status || ' ' || amount_sar FROM financial_kpi.financial_commitment
            WHERE project_id = '{projectId}' AND commitment_type = 'APPROVED_BUDGET' ORDER BY version_no
            """);

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this ChangeRequestTestHost host, string subjectModule, Guid subjectId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = '{subjectModule}' AND subject_id = '{subjectId}' ORDER BY occurred_at, id");

    /// <summary>Every audit event of the project since <paramref name="since"/>, as "module event", in order.</summary>
    public static Task<IReadOnlyList<string>> ProjectEventsSinceAsync(this ChangeRequestTestHost host, Guid projectId, DateTimeOffset since) =>
        host.Database.QueryAsync($"""
            SELECT subject_module || ' ' || event_type FROM audit_activity.audit_event
            WHERE scope_project_id = '{projectId}' AND occurred_at > '{since.UtcDateTime:O}' ORDER BY occurred_at, id
            """);

    /// <summary>The database's clock: what a later audit event is compared with.</summary>
    public static async Task<DateTimeOffset> NowAsync(this ChangeRequestTestHost host) =>
        DateTimeOffset.Parse(Assert.Single(await host.Database.QueryAsync("SELECT to_char(clock_timestamp() AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US\"Z\"')")), CultureInfo.InvariantCulture);

    /// <summary>The SQL error a statement raises, or null when it succeeds; it is rolled back either way.</summary>
    public static async Task<string?> RefusedAsync(this ChangeRequestTestHost host, string sql)
    {
        try
        {
            await host.Database.ExecuteRolledBackAsync(sql);
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

    public static async Task<ChangeSessions> SignInAsync(this HttpClient client) =>
        new(
            (await client.SignInOrFailAsync(8)).AccessToken,
            (await client.SignInOrFailAsync(5)).AccessToken,
            (await client.SignInOrFailAsync(2)).AccessToken,
            (await client.SignInOrFailAsync(3)).AccessToken);
}

/// <summary>
/// The people most tests act as: the entity Project Manager who raises and keeps the schedule and budget (local.r08), an internal Project
/// Manager (local.r05), AHDA's change officer, who reviews, decides and implements (local.r02), and the Department Manager (local.r03).
/// </summary>
internal sealed record ChangeSessions(string EntityManager, string InternalManager, string Officer, string DepartmentManager);
