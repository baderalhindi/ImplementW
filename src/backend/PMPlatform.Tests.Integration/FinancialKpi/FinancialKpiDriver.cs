using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.ChangeRequest.Fixtures;
using PMPlatform.Tests.Integration.DocumentManagement;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.FinancialKpi;

/// <summary>WF-14 as the SPA reaches it, WF-11's side of an approval driven in process, and the fixtures no API creates.</summary>
internal static class FinancialKpiDriver
{
    public const string SourceModes = "/api/v1/financial-source-modes";
    public const string Commitments = "/api/v1/financial-commitments";
    public const string Updates = "/api/v1/financial-progress-updates";
    public const string Snapshots = "/api/v1/published-financial-snapshots";
    public const string Positions = "/api/v1/financial-positions";
    public const string FinancialAggregates = "/api/v1/financial-portfolio-aggregates";
    public const string Assignments = "/api/v1/kpi-assignments";
    public const string Targets = "/api/v1/kpi-target-versions";
    public const string Measurements = "/api/v1/kpi-measurements";
    public const string KpiAggregates = "/api/v1/kpi-portfolio-aggregates";

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A new ACTIVE project of the test department and <paramref name="entityId"/> (the active entity by default), managed by local.r08.</summary>
    public static async Task<Guid> ProjectAsync(this FinancialKpiTestHost host, Guid? entityId = null)
    {
        Guid id = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', 'PRJ-F{id.ToString("N")[..12]}', 'Financial test project', 'en', '{FinancialKpiTestHost.ClassificationId}', '{FinancialKpiTestHost.DepartmentId}',
                    '{entityId ?? FinancialKpiTestHost.EntityId}', '{Person(8)}', 'ACTIVE', '{host.StandardProfileId}', 'ENTITY_MANAGED', now(),
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """);
        return id;
    }

    /// <summary>
    /// WF-02's reporting periods for the project, as WF-02 generates them (edge 13): <paramref name="count"/> consecutive 7-day
    /// periods, the last of which has begun today. Returns them earliest first.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> PeriodsAsync(this FinancialKpiTestHost host, Guid projectId, int count)
    {
        List<Guid> ids = [];
        for (int i = count - 1; i >= 0; i--)
        {
            Guid id = Guid.NewGuid();
            DateOnly start = Today.AddDays(-7 * i);
            await host.Database.ExecuteAsync($"""
                INSERT INTO progress.reporting_cycle (id, project_id, period_start, period_end, due_date, status, created_at, created_by, updated_at, updated_by)
                VALUES ('{id}', '{projectId}', '{Iso(start)}', '{Iso(start.AddDays(6))}', '{Iso(start.AddDays(6))}', 'OPEN', now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
                """);
            ids.Add(id);
        }

        return ids;
    }

    public static async Task<JsonObject> CreatedOrFailAsync(this HttpClient client, string token, string collection, object body)
    {
        using HttpResponseMessage created = await client.PostAsync(collection, token, body);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"POST {collection}: {(int)created.StatusCode} {await created.Content.ReadAsStringAsync()}");
        return await created.ReadObjectAsync();
    }

    public static async Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, string path, object? body = null)
    {
        using HttpResponseMessage response = await client.PostAsync(path, token, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"POST {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    /// <summary>Reads a resource and replaces it with <paramref name="body"/> under its ETag, asserting 200.</summary>
    public static async Task<JsonObject> PutOrFailAsync(this HttpClient client, string token, string path, object body)
    {
        using HttpResponseMessage current = await client.GetAsync(path, token);
        using HttpResponseMessage replaced = await client.PutAsync(path, token, body, AdministrationApi.ETagOf(current));
        Assert.True(replaced.StatusCode == HttpStatusCode.OK, $"PUT {path}: {(int)replaced.StatusCode} {await replaced.Content.ReadAsStringAsync()}");
        return await replaced.ReadObjectAsync();
    }

    public static async Task<HttpResponseMessage> PutAsync(this HttpClient client, string token, string path, object body)
    {
        using HttpResponseMessage current = await client.GetAsync(path, token);
        return await client.PutAsync(path, token, body, AdministrationApi.ETagOf(current));
    }

    public static async Task<JsonObject> GetOrFailAsync(this HttpClient client, string token, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path, token);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    public static async Task<JsonArray> ItemsAsync(this HttpClient client, string token, string collection, string query) =>
        (await client.GetOrFailAsync(token, $"{collection}?{query}&pageSize=200"))["items"]!.AsArray();

    public static async Task<(HttpStatusCode Status, string? Code)> RefusalAsync(this HttpResponseMessage response) =>
        (response.StatusCode, (await response.ReadObjectAsync())["code"]?.GetValue<string>());

    public static string Text(this JsonNode node, string property) => node[property]!.GetValue<string>();

    // Financial Progress.

    public static object Update(string? actual, string? forecast, string valueStatus, string? sourceReference = null) => new
    {
        actualExpenditureToDateSar = actual,
        forecastAtCompletionSar = forecast,
        valueStatus,
        sourceReference,
        asOfDate = Iso(Today),
    };

    /// <summary>Starts the next period's update as local.r08, enters the figures, submits it; returns its id.</summary>
    public static async Task<Guid> SubmittedUpdateAsync(this HttpClient client, Sessions sessions, Guid projectId, object figures)
    {
        Guid id = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Updates, new { projectId }));
        await client.PutOrFailAsync(sessions.ProjectManager, $"{Updates}/{id}", figures);
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{Updates}/{id}/submit");
        return id;
    }

    /// <summary>Submits the figures for the next period as local.r08 and publishes them as local.r02.</summary>
    public static async Task<Guid> PublishedUpdateAsync(this HttpClient client, Sessions sessions, Guid projectId, object figures)
    {
        Guid id = await client.SubmittedUpdateAsync(sessions, projectId, figures);
        await client.CommandOrFailAsync(sessions.Portfolio, $"{Updates}/{id}/start-review");
        await client.CommandOrFailAsync(sessions.Portfolio, $"{Updates}/{id}/publish");
        return id;
    }

    /// <summary>Opens an Approved Budget version as local.r08, attaches a CLEAN referenced document, submits it and has local.r02 approve it.</summary>
    /// <summary>
    /// An Approved Budget version of the project, approved and ACTIVE. A change to an ACTIVE budget implements a WF-08 change authorisation
    /// (TASK-060), issued for it by <see cref="ChangeAuthorizationFixture"/>.
    /// </summary>
    public static async Task<Guid> ActiveBudgetAsync(this FinancialKpiTestHost host, HttpClient client, Sessions sessions, Guid projectId, string amountSar)
    {
        Guid id = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Commitments, Budget(projectId, amountSar)));
        await host.AttachDocumentAsync(client, sessions.ProjectManager, projectId, id);
        bool isChange = (await host.Database.QueryAsync(
            $"SELECT id::text FROM financial_kpi.financial_commitment WHERE project_id = '{projectId}' AND status = 'ACTIVE' AND commitment_type = 'APPROVED_BUDGET'")).Count > 0;
        Guid? authorization = isChange ? await host.Database.IssueCommitmentChangeAsync(projectId) : null;
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{Commitments}/{id}/submit", new { changeAuthorizationId = authorization });
        await host.DecideAndDeliverAsync(FinancialKpiApprovalRouting.CommitmentType, id, ApprovalTaskDecision.Approve);
        return id;
    }

    public static object Budget(Guid projectId, string amountSar) => new { projectId, amountSar, sourceReference = "Board minute 12/2026", asOfDate = Iso(Today) };

    /// <summary>Uploads a budget letter to the project, scans it clean, and attaches it to the commitment version as its reference.</summary>
    public static async Task AttachDocumentAsync(this FinancialKpiTestHost host, HttpClient client, string token, Guid projectId, Guid commitmentId)
    {
        using HttpResponseMessage uploaded = await client.UploadAsync(
            token, DocumentDriver.Text($"Budget approval {Guid.NewGuid()}"), projectId, FinancialKpiTestHost.Internal, documentType: FinancialKpiTestHost.DocumentTypeId);
        Assert.True(uploaded.StatusCode == HttpStatusCode.Created, $"upload: {(int)uploaded.StatusCode} {await uploaded.Content.ReadAsStringAsync()}");
        JsonObject document = await uploaded.ReadObjectAsync();
        await host.Api.ScanAsync();
        await client.CreatedOrFailAsync(token, $"{Commitments}/{commitmentId}/documents", new
        {
            documentId = AdministrationApi.IdOf(document),
            documentVersionId = document["latestVersion"]!.Text("id"),
            evidenceTypeItemId = FinancialKpiTestHost.EvidenceTypeId,
        });
    }

    // KPI Performance.

    public static async Task<Guid> AssignmentAsync(this HttpClient client, Sessions sessions, Guid projectId, Guid kpiDefinitionId) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Assignments, new
        {
            projectId,
            kpiDefinitionId,
            ownerUserId = Person(8),
            measurementFrequencyItemId = FinancialKpiTestHost.MonthlyId,
        }));

    /// <summary>Opens a target version as local.r08, submits it, and has local.r02 approve it through WF-11.</summary>
    public static async Task<Guid> ActiveTargetAsync(this FinancialKpiTestHost host, HttpClient client, Sessions sessions, Guid assignmentId, decimal target, decimal green, decimal amber)
    {
        Guid id = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Targets, new
        {
            kpiAssignmentId = assignmentId,
            targetValue = target,
            greenThreshold = green,
            amberThreshold = amber,
        }));
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{Targets}/{id}/submit");
        await host.DecideAndDeliverAsync(FinancialKpiApprovalRouting.TargetVersionType, id, ApprovalTaskDecision.Approve);
        return id;
    }

    public static object Measurement(Guid assignmentId, DateOnly periodStart, decimal? value, string valueStatus = "MEASURED") => new
    {
        kpiAssignmentId = assignmentId,
        periodStart = Iso(periodStart),
        periodEnd = Iso(periodStart.AddDays(29)),
        measuredValue = value,
        valueStatus,
        asOfDate = Iso(Today),
    };

    /// <summary>Records a measurement as local.r08, submits it, and has local.r02 publish it.</summary>
    public static async Task<JsonObject> PublishedMeasurementAsync(this HttpClient client, Sessions sessions, Guid assignmentId, DateOnly periodStart, decimal? value, string valueStatus = "MEASURED")
    {
        Guid id = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, Measurements, Measurement(assignmentId, periodStart, value, valueStatus)));
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{Measurements}/{id}/submit");
        return await client.CommandOrFailAsync(sessions.Portfolio, $"{Measurements}/{id}/publish");
    }

    // WF-11 and the outbox, in process.

    /// <summary>Decides the open task of the subject's latest run as local.r02 and delivers the outcome.</summary>
    public static async Task DecideAndDeliverAsync(this FinancialKpiTestHost host, string subjectType, Guid subjectId, ApprovalTaskDecision decision, string? reason = null)
    {
        ApprovalInstanceDetail run = (await host.WithScopeAsync(services => services.GetRequiredService<IApprovalRequests>()
            .FindBySubjectAsync(FinancialKpiApprovalRouting.SubjectModule, subjectType, subjectId, CancellationToken.None)))[^1];
        AdministrationResult<ApprovalInstanceDetail> decided = await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
            Person(2), run.Tasks.Single(t => t.Status == Domain.Approval.ApprovalTaskStatus.Pending).Id, decision,
            reason is null ? null : new NarrativeText(reason, Language.En), CancellationToken.None));
        Assert.True(decided.Succeeded, $"Decision refused: {decided.Error}");
        Guid message = Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{run.Id}-outcome'")));
        Assert.True(await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(message, CancellationToken.None));
    }

    public static async Task<T> WithScopeAsync<T>(this FinancialKpiTestHost host, Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>The whole row as the database holds it, as JSON text, without the columns <paramref name="except"/>.</summary>
    public static async Task<string> RowAsync(this FinancialKpiTestHost host, string table, Guid id, params string[] except) =>
        Assert.Single(await host.Database.QueryAsync(
            $"SELECT (to_jsonb(t) - ARRAY[{string.Join(", ", except.Select(c => $"'{c}'").DefaultIfEmpty("''"))}]::text[])::text FROM financial_kpi.{table} t WHERE id = '{id}'"));

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this FinancialKpiTestHost host, Guid subjectId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'FinancialKpi' AND subject_id = '{subjectId}' ORDER BY occurred_at, id");

    /// <summary>Runs a statement as a raw writer would, and returns the constraint or error it is refused with, or null.</summary>
    public static async Task<string?> RefusedAsync(this FinancialKpiTestHost host, string sql)
    {
        try
        {
            await host.Database.ExecuteAsync(sql);
            return null;
        }
        catch (Npgsql.PostgresException exception)
        {
            return exception.ConstraintName ?? exception.SqlState;
        }
    }

    /// <summary>The statement with the guard triggers off, in a transaction of its own: what the CHECK constraints alone refuse.</summary>
    public static string Unguarded(string sql) => $"BEGIN; SET LOCAL session_replication_role = replica; {sql}; COMMIT;";

    public static async Task<Sessions> SignInAsync(this HttpClient client) =>
        new((await client.SignInOrFailAsync(8)).AccessToken, (await client.SignInOrFailAsync(2)).AccessToken);
}

/// <summary>The people most tests act as: the entity Project Manager (local.r08), who enters, and local.r02, who reviews and decides.</summary>
internal sealed record Sessions(string ProjectManager, string Portfolio);
