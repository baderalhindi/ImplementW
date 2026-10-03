using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Milestone;

/// <summary>WF-03's milestones and WF-05's achievements as the SPA reaches them, WF-11's side of a review driven in process, and the fixtures no API creates.</summary>
internal static class MilestoneDriver
{
    public const string Milestones = "/api/v1/project-milestones";
    public const string Achievements = "/api/v1/milestone-achievements";

    /// <summary>Claims are dated in the past: an achievement is claimed once it has happened.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static Guid Person(int n) => Guid.Parse(IdentityDatabase.UserId(n));

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A new project in <paramref name="state"/>, of the test department and the active entity, managed by <paramref name="projectManager"/>.</summary>
    public static async Task<Guid> ProjectAsync(this MilestoneTestHost host, string state = "ACTIVE", int projectManager = 8)
    {
        Guid id = Guid.NewGuid();
        string activatedAt = state == "ACTIVE" ? "now()" : "NULL";
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project (id, formal_project_id, title, title_lang, classification_item_id, department_id, external_entity_id, project_manager_user_id,
                                         lifecycle_state, governance_profile_item_id, participation_mode, activated_at, created_at, created_by, updated_at, updated_by)
            VALUES ('{id}', 'PRJ-M{id.ToString("N")[..12]}', 'Milestone test project', 'en', '{MilestoneTestHost.ClassificationId}', '{MilestoneTestHost.DepartmentId}',
                    '{MilestoneTestHost.EntityId}', '{Person(projectManager)}', '{state}', '{host.StandardProfileId}', 'ENTITY_MANAGED', {activatedAt},
                    now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')
            """);
        return id;
    }

    /// <summary>A project with an initialized schedule.</summary>
    public static async Task<Guid> ScheduledProjectAsync(this MilestoneTestHost host, HttpClient client, string token, string state = "ACTIVE")
    {
        Guid projectId = await host.ProjectAsync(state);
        await client.CreatedOrFailAsync(token, ScheduleDriver.Schedules, new { projectId });
        return projectId;
    }

    public static object Milestone(Guid projectId, Guid? category = null, DateOnly? forecast = null, Guid? activityId = null, string title = "Works handed over") => new
    {
        projectId,
        scheduleActivityId = activityId,
        title = new { text = title, language = "en" },
        milestoneCategoryItemId = category ?? MilestoneTestHost.GeneralCategoryId,
        forecastDate = Iso(forecast ?? Today.AddDays(30)),
    };

    public static async Task<Guid> MilestoneAsync(this HttpClient client, string token, Guid projectId, Guid? category = null, DateOnly? forecast = null) =>
        AdministrationApi.IdOf(await client.CreatedOrFailAsync(token, Milestones, Milestone(projectId, category, forecast)));

    public static object Claim(Guid milestoneId, DateOnly claimed, string? narrative = "Handed over to the operator") => new
    {
        projectMilestoneId = milestoneId,
        claimedAchievementDate = Iso(claimed),
        narrative = narrative is null ? null : new { text = narrative, language = "en" },
    };

    /// <summary>Opens the milestone's next revision as a DRAFT, asserting 201.</summary>
    public static Task<JsonObject> ClaimAsync(this HttpClient client, string token, Guid milestoneId, DateOnly claimed) =>
        client.CreatedOrFailAsync(token, Achievements, Claim(milestoneId, claimed));

    /// <summary>Opens a revision and submits it to WF-11; returns it as submitted.</summary>
    public static async Task<JsonObject> SubmittedClaimAsync(this HttpClient client, string token, Guid milestoneId, DateOnly claimed)
    {
        Guid id = AdministrationApi.IdOf(await client.ClaimAsync(token, milestoneId, claimed));
        return await client.CommandOrFailAsync(token, $"{Achievements}/{id}/submit");
    }

    /// <summary>The revision as the API returns it, asserting 200.</summary>
    public static async Task<JsonObject> AchievementAsync(this HttpClient client, string token, Guid achievementId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{Achievements}/{achievementId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadObjectAsync();
    }

    /// <summary>The shared milestone as WF-03's API returns it, asserting 200.</summary>
    public static async Task<JsonObject> MilestoneOrFailAsync(this HttpClient client, string token, Guid milestoneId)
    {
        using HttpResponseMessage response = await client.GetAsync($"{Milestones}/{milestoneId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadObjectAsync();
    }

    /// <summary>The first page of revisions the query names, asserting 200.</summary>
    public static async Task<JsonArray> AchievementPageAsync(this HttpClient client, string token, string query)
    {
        using HttpResponseMessage response = await client.GetAsync($"{Achievements}?{query}&pageSize=200", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadObjectAsync())["items"]!.AsArray();
    }

    /// <summary>The revision's review runs, oldest revision first.</summary>
    public static Task<IReadOnlyList<ApprovalInstanceDetail>> RunsAsync(this MilestoneTestHost host, Guid achievementId) =>
        host.RunsAsync(MilestoneApprovalRouting.SubjectModule, MilestoneApprovalRouting.SubjectType, achievementId);

    public static Task<IReadOnlyList<ApprovalInstanceDetail>> RunsAsync(this MilestoneTestHost host, string subjectModule, string subjectType, Guid subjectId) =>
        host.WithScopeAsync(services => services.GetRequiredService<IApprovalRequests>().FindBySubjectAsync(subjectModule, subjectType, subjectId, CancellationToken.None));

    /// <summary>Decides the open task of the revision's run as local.r02 and delivers the outcome: what the approver and the outbox do.</summary>
    public static Task DecideAndDeliverAsync(this MilestoneTestHost host, Guid achievementId, ApprovalTaskDecision decision, string? reason = null) =>
        host.DecideAndDeliverAsync(MilestoneApprovalRouting.SubjectModule, MilestoneApprovalRouting.SubjectType, achievementId, decision, reason);

    /// <summary>Decides the open task of the subject's only run as local.r02 and delivers the outcome.</summary>
    public static async Task DecideAndDeliverAsync(
        this MilestoneTestHost host, string subjectModule, string subjectType, Guid subjectId, ApprovalTaskDecision decision, string? reason = null)
    {
        ApprovalInstanceDetail run = Assert.Single(await host.RunsAsync(subjectModule, subjectType, subjectId));
        AdministrationResult<ApprovalInstanceDetail> decided = await host.WithScopeAsync(services => services.GetRequiredService<IApprovalWorkflowService>().DecideAsync(
            Person(2), run.Tasks.Single(t => t.Status == Domain.Approval.ApprovalTaskStatus.Pending).Id, decision,
            reason is null ? null : new NarrativeText(reason, Language.En), CancellationToken.None));
        Assert.True(decided.Succeeded, $"Decision refused: {decided.Error}");
        Assert.True(await host.DeliverAsync(run.Id));
    }

    /// <summary>Dispatches the run's outcome message now; false when the consumer threw and the message waits for a retry.</summary>
    public static async Task<bool> DeliverAsync(this MilestoneTestHost host, Guid runId)
    {
        Guid message = Guid.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT id::text FROM common.outbox_message WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-{runId}-outcome'")));
        return await host.Api.Services.GetRequiredService<IOutboxDispatcher>().DispatchAsync(message, CancellationToken.None);
    }

    public static async Task<T> WithScopeAsync<T>(this MilestoneTestHost host, Func<IServiceProvider, Task<T>> action)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>Each revision of the milestone as "revision STATUS", in revision order, as the database holds them.</summary>
    public static Task<IReadOnlyList<string>> RevisionStatesAsync(this MilestoneTestHost host, Guid milestoneId) =>
        host.Database.QueryAsync($"SELECT revision_no || ' ' || status FROM milestone.milestone_achievement WHERE project_milestone_id = '{milestoneId}' ORDER BY revision_no");

    /// <summary>The whole row as the database holds it, as JSON text without the columns <paramref name="except"/>.</summary>
    public static async Task<string> RowAsync(this MilestoneTestHost host, Guid achievementId, params string[] except) =>
        Assert.Single(await host.Database.QueryAsync(
            $"SELECT (to_jsonb(a) - ARRAY[{string.Join(", ", except.Select(c => $"'{c}'"))}]::text[])::text FROM milestone.milestone_achievement a WHERE id = '{achievementId}'"));

    public static Task<IReadOnlyList<string>> AuditEventsAsync(this MilestoneTestHost host, string module, Guid subjectId) =>
        host.Database.QueryAsync($"SELECT event_type FROM audit_activity.audit_event WHERE subject_module = '{module}' AND subject_id = '{subjectId}' ORDER BY occurred_at, id");

    public static async Task<MilestoneSessions> SignInAsync(this HttpClient client) =>
        new((await client.SignInOrFailAsync(8)).AccessToken, (await client.SignInOrFailAsync(2)).AccessToken);
}

/// <summary>The people most tests act as: the entity Project Manager (local.r08), who plans and claims, and local.r02, who decides.</summary>
internal sealed record MilestoneSessions(string ProjectManager, string Portfolio);
