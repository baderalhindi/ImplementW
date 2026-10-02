using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Project;

/// <summary>WF-01 as the SPA reaches it, and WF-11's side of the review driven in process; each in-process call is its own scope.</summary>
internal static class ProjectDriver
{
    public const string Projects = "/api/v1/projects";

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    /// <summary>
    /// A complete registration of an entity-managed project of <see cref="ProjectTestHost.EntityId"/>, planned to start
    /// <paramref name="startsInDays"/> from today; <paramref name="change"/> adjusts the body before it is sent.
    /// </summary>
    public static JsonObject Registration(this ProjectTestHost host, int startsInDays = 30, Action<JsonObject>? change = null)
    {
        DateOnly start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(startsInDays);
        JsonObject body = new()
        {
            ["title"] = new JsonObject { ["text"] = "Regional road upgrade", ["language"] = "en" },
            ["classificationItemId"] = ProjectTestHost.ClassificationId.ToString(),
            ["departmentId"] = ProjectTestHost.DepartmentId.ToString(),
            ["externalEntityId"] = ProjectTestHost.EntityId.ToString(),
            ["participationMode"] = "ENTITY_MANAGED",
            ["governanceProfileItemId"] = host.GovernanceProfileId.ToString(),
            ["registrationBudgetSar"] = "12500000.00",
            ["plannedStartDate"] = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["plannedEndDate"] = start.AddYears(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["regionItemId"] = ProjectTestHost.RegionId.ToString(),
            ["cityItemId"] = ProjectTestHost.CityId.ToString(),
        };
        change?.Invoke(body);
        return body;
    }

    /// <summary>Creates the registration and returns the new DRAFT, asserting 201.</summary>
    public static async Task<JsonObject> CreateOrFailAsync(this HttpClient client, string token, JsonObject registration)
    {
        using HttpResponseMessage created = await client.PostAsync(Projects, token, registration);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return await created.ReadObjectAsync();
    }

    public static Task<HttpResponseMessage> DeleteProjectAsync(this HttpClient client, Guid projectId, string token) =>
        client.SendAsync(HttpMethod.Delete, $"{Projects}/{projectId}", token);

    /// <summary>Sends the command and returns the project, asserting 200.</summary>
    public static async Task<JsonObject> CommandOrFailAsync(this HttpClient client, string token, Guid projectId, string command, object? body = null)
    {
        using HttpResponseMessage response = await client.PostAsync($"{Projects}/{projectId}/{command}", token, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{command}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return await response.ReadObjectAsync();
    }

    /// <summary>A new entity project by local.r08, submitted naming local.r08 as its manager, and taken into review by local.r03.</summary>
    public static async Task<Guid> UnderReviewAsync(this ProjectTestHost host, HttpClient client, Sessions sessions, int startsInDays = 30)
    {
        Guid projectId = AdministrationApi.IdOf(await client.CreateOrFailAsync(sessions.Entity, host.Registration(startsInDays)));
        await client.CommandOrFailAsync(sessions.Entity, projectId, "submit", new { projectManagerUserId = Person(8) });
        await client.CommandOrFailAsync(sessions.Reviewer, projectId, "start-review");
        return projectId;
    }

    public static async Task<Sessions> SignInAsync(this HttpClient client) =>
        new((await client.SignInOrFailAsync(2)).AccessToken, (await client.SignInOrFailAsync(3)).AccessToken, (await client.SignInOrFailAsync(8)).AccessToken);

    public static string Status(this JsonObject project) => project["status"]!.GetValue<string>();

    public static string? FormalProjectId(this JsonObject project) => project["formalProjectId"]?.GetValue<string>();

    /// <summary>The project's review runs, oldest revision first.</summary>
    public static Task<IReadOnlyList<ApprovalInstanceDetail>> RunsAsync(this ProjectTestHost host, Guid projectId) =>
        host.WithScopeAsync(services => services.GetRequiredService<IApprovalRequests>()
            .FindBySubjectAsync(ProjectApprovalRouting.SubjectModule, ProjectApprovalRouting.SubjectType, projectId, CancellationToken.None));

    /// <summary>Decides the open task of the project's latest run as local.r02 and delivers the outcome: what the review's approver and the outbox do.</summary>
    public static async Task DecideAndDeliverAsync(this ProjectTestHost host, Guid projectId, ApprovalTaskDecision decision, string? reason = null)
    {
        ApprovalInstanceDetail run = (await host.RunsAsync(projectId))[^1];
        AdministrationResult<ApprovalInstanceDetail> decided = await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
            Person(2), run.Tasks.Single(t => t.Status == Domain.Approval.ApprovalTaskStatus.Pending).Id, decision,
            reason is null ? null : new NarrativeText(reason, Language.En), CancellationToken.None));
        Assert.True(decided.Succeeded, $"Decision refused: {decided.Error}");
        Assert.True(await host.DeliverAsync(run.Id));
    }

    /// <summary>Dispatches the run's outcome message now.</summary>
    public static async Task<bool> DeliverAsync(this ProjectTestHost host, Guid runId)
    {
        Guid message = Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{runId}-outcome'")));
        return await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(message, CancellationToken.None);
    }

    /// <summary>One pass of every background worker that runs on a schedule: the outbox dispatcher and approval maintenance.</summary>
    public static async Task RunWorkersAsync(this ProjectTestHost host)
    {
        await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchDueAsync(100, CancellationToken.None);
        await host.WithScopeAsync(services => services.GetRequiredService<IApprovalMaintenance>().RunAsync(100, CancellationToken.None));
    }

    public static async Task<T> WithScopeAsync<T>(this ProjectTestHost host, Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>The project row as the database holds it, every column, as JSON.</summary>
    public static async Task<JsonObject> RowAsync(this ProjectTestHost host, Guid projectId) =>
        JsonNode.Parse(Assert.Single(await host.Database.QueryAsync($"SELECT to_jsonb(p)::text FROM project.project p WHERE p.id = '{projectId}'")))!.AsObject();

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this ProjectTestHost host, Guid projectId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = 'Project' AND subject_id = '{projectId}' ORDER BY occurred_at, id");
}

/// <summary>The access tokens of the three people most tests act as.</summary>
internal sealed record Sessions(string Approver, string Reviewer, string Entity);
