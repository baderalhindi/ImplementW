using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Milestone;

/// <summary>
/// WF-03's side of the shared milestone (ICD-04, TASK-050): its schedule representation and dates, kept by the Project Manager on
/// the schedule, frozen with the plan while a baseline candidate is with WF-11, and copied into the baseline as it activates.
/// </summary>
[Collection(MilestoneSuite.Name)]
public sealed class ProjectMilestoneTests(MilestoneTestHost host)
{
    [Fact]
    public async Task TheProjectManagerPlansEditsAndCancelsAMilestone()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid activityId = await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);

        using HttpResponseMessage created = await client.PostAsync(
            MilestoneDriver.Milestones, sessions.ProjectManager, MilestoneDriver.Milestone(projectId, MilestoneTestHost.HandoverCategoryId, MilestoneDriver.Today.AddDays(40)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        JsonObject milestone = await created.ReadObjectAsync();
        Guid milestoneId = AdministrationApi.IdOf(milestone);
        Assert.Equal(("PLANNED", projectId.ToString(), null, null), (milestone.Text("status"), milestone.Text("projectId"), milestone["baselineId"], milestone["forecastVarianceDays"]));

        object changes = new
        {
            scheduleActivityId = activityId,
            title = new { text = "Works complete", language = "en" },
            milestoneCategoryItemId = MilestoneTestHost.GeneralCategoryId,
            forecastDate = MilestoneDriver.Iso(MilestoneDriver.Today.AddDays(45)),
            sortOrder = 2,
        };
        using HttpResponseMessage edited = await client.PutAsync($"{MilestoneDriver.Milestones}/{milestoneId}", sessions.ProjectManager, changes, AdministrationApi.ETagOf(created));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        JsonObject after = await edited.ReadObjectAsync();
        Assert.Equal((activityId.ToString(), "Works complete", MilestoneDriver.Iso(MilestoneDriver.Today.AddDays(45))),
            (after.Text("scheduleActivityId"), after["title"]!.Text("text"), after.Text("forecastDate")));

        JsonObject cancelled = await client.CommandOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Milestones}/{milestoneId}/cancel");
        Assert.Equal("CANCELLED", cancelled.Text("status"));
        using (HttpResponseMessage again = await client.PostAsync($"{MilestoneDriver.Milestones}/{milestoneId}/cancel", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }

        using HttpResponseMessage current = await client.GetAsync($"{MilestoneDriver.Milestones}/{milestoneId}", sessions.ProjectManager);
        using (HttpResponseMessage history = await client.PutAsync($"{MilestoneDriver.Milestones}/{milestoneId}", sessions.ProjectManager, changes, AdministrationApi.ETagOf(current)))
        {
            Assert.Equal((HttpStatusCode.Conflict, ScheduleErrorCodes.NotEditable), await history.RefusalAsync());
        }

        Assert.Equal(["Schedule.MilestoneCreated", "Schedule.MilestoneChanged", "Schedule.MilestoneCancelled"], await host.AuditEventsAsync("Schedule", milestoneId));
    }

    /// <summary>A milestone names a PUBLISHED MILESTONE_CATEGORY item, and only an activity of its own project's schedule.</summary>
    [Fact]
    public async Task AMilestoneNamesAPublishedCategoryAndAnActivityOfItsOwnSchedule()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid otherActivity = await client.ActivityAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager), "1", 0, 5);

        using (HttpResponseMessage draftCategory = await client.PostAsync(MilestoneDriver.Milestones, sessions.ProjectManager, MilestoneDriver.Milestone(projectId, MilestoneTestHost.DraftCategoryId)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, ScheduleErrorCodes.MilestoneCategoryInvalid), await draftCategory.RefusalAsync());
        }

        using (HttpResponseMessage foreign = await client.PostAsync(MilestoneDriver.Milestones, sessions.ProjectManager, MilestoneDriver.Milestone(projectId, activityId: otherActivity)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, ScheduleErrorCodes.MilestoneActivityInvalid), await foreign.RefusalAsync());
        }

        Guid unscheduled = await host.ProjectAsync();
        using HttpResponseMessage noSchedule = await client.PostAsync(MilestoneDriver.Milestones, sessions.ProjectManager, MilestoneDriver.Milestone(unscheduled));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, ScheduleErrorCodes.NotInitialized), await noSchedule.RefusalAsync());
    }

    /// <summary>
    /// The baseline copies each live milestone's forecast as its planned date when it activates; a later reforecast shows as
    /// variance against that copy, which never moves. While the candidate is with WF-11 the milestones are frozen.
    /// </summary>
    [Fact]
    public async Task TheBaselineFreezesAndCopiesTheMilestoneDates()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);
        DateOnly planned = MilestoneDriver.Today.AddDays(30);
        Guid kept = await client.MilestoneAsync(sessions.ProjectManager, projectId, forecast: planned);
        Guid dropped = await client.MilestoneAsync(sessions.ProjectManager, projectId, forecast: planned);
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Milestones}/{dropped}/cancel");

        Guid baselineId = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId));
        using (HttpResponseMessage frozen = await client.PostAsync(MilestoneDriver.Milestones, sessions.ProjectManager, MilestoneDriver.Milestone(projectId)))
        {
            Assert.Equal((HttpStatusCode.Conflict, ScheduleErrorCodes.NotEditable), await frozen.RefusalAsync());
        }

        Npgsql.PostgresException guarded = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            host.Database.ExecuteRolledBackAsync($"UPDATE schedule.project_milestone SET forecast_date = forecast_date + 1 WHERE id = '{kept}'"));
        Assert.Contains("frozen", guarded.MessageText, StringComparison.Ordinal);

        await host.DecideAndDeliverAsync(ScheduleApprovalRouting.SubjectModule, ScheduleApprovalRouting.SubjectType, baselineId, ApprovalTaskDecision.Approve);

        using (HttpResponseMessage copy = await client.GetAsync($"{ScheduleDriver.Baselines}/{baselineId}/baseline-milestones", sessions.ProjectManager))
        {
            JsonNode item = Assert.Single((await copy.ReadObjectAsync())["items"]!.AsArray())!;
            Assert.Equal((kept.ToString(), MilestoneDriver.Iso(planned)), (item.Text("projectMilestoneId"), item.Text("plannedDate")));
        }

        // Reforecast five days later: the baseline's date stays, and the variance shows the slip.
        using HttpResponseMessage current = await client.GetAsync($"{MilestoneDriver.Milestones}/{kept}", sessions.ProjectManager);
        using HttpResponseMessage reforecast = await client.PutAsync($"{MilestoneDriver.Milestones}/{kept}", sessions.ProjectManager, new
        {
            title = new { text = "Works handed over", language = "en" },
            milestoneCategoryItemId = MilestoneTestHost.GeneralCategoryId,
            forecastDate = MilestoneDriver.Iso(planned.AddDays(5)),
        }, AdministrationApi.ETagOf(current));
        JsonObject slipped = await reforecast.ReadObjectAsync();
        Assert.Equal((baselineId.ToString(), MilestoneDriver.Iso(planned), 5), (slipped.Text("baselineId"), slipped.Text("baselinePlannedDate"), slipped.Days("forecastVarianceDays")));

        JsonArray listed = await client.ItemsAsync(sessions.ProjectManager, MilestoneDriver.Milestones, projectId);
        Assert.Equal([kept.ToString(), dropped.ToString()], listed.Select(m => m!.Text("id")).Order());
    }

    /// <summary>A project the caller may not see has no milestones for them (R-3), and its milestone does not exist for them (R-47).</summary>
    [Fact]
    public async Task AnotherProjectsMilestonesAreInvisible()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, projectId);
        string other = (await client.SignInOrFailAsync(5)).AccessToken;

        Assert.Empty(await client.ItemsAsync(other, MilestoneDriver.Milestones, projectId));
        using HttpResponseMessage hidden = await client.GetAsync($"{MilestoneDriver.Milestones}/{milestoneId}", other);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }
}
