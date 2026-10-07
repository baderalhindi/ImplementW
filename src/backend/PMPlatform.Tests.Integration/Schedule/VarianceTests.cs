using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.ChangeRequest.Fixtures;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>
/// The acceptance criterion's second half, end to end: variance and Schedule Health use the Approved Baseline as the
/// reference, never the Current Forecast against itself — and not the working plan either, which moves on after approval.
/// </summary>
[Collection(ScheduleSuite.Name)]
public sealed class VarianceTests(ScheduleTestHost host)
{
    [Fact]
    public async Task VarianceIsMeasuredAgainstTheApprovedBaselineNeverTheForecastItself()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid works = await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);

        // Before any baseline there is nothing to forecast against, and no variance: UNKNOWN, not GREEN.
        using (HttpResponseMessage tooEarly = await client.PostAsync($"{ScheduleDriver.Activities}/{works}/reforecast", sessions.ProjectManager, Forecast(0, 9)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_ACTIVE_BASELINE_REQUIRED"), await tooEarly.RefusalAsync());
        }

        Assert.Equal("UNKNOWN", (await HealthAsync(client, sessions, projectId)).Text("scheduleHealth"));

        Guid baseline = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId));
        await host.DecideAndDeliverAsync(baseline, ApprovalTaskDecision.Approve);
        JsonObject onPlan = (await client.ActivitiesAsync(sessions.ProjectManager, projectId))["1"];
        Assert.Equal((baseline.ToString(), 0, 0), (onPlan.Text("baselineId"), onPlan.Days("startVarianceDays"), onPlan.Days("finishVarianceDays")));
        Assert.Equal(("GREEN", 0), ((await HealthAsync(client, sessions, projectId)).Text("scheduleHealth"), (await HealthAsync(client, sessions, projectId)).Days("finishVarianceDays")));

        // The forecast slips by 6 working days: the variance is measured from the frozen baseline dates.
        JsonObject slipped = await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Activities}/{works}/reforecast", Forecast(2, 15));
        Assert.Equal((ScheduleDriver.Iso(ScheduleDriver.Day1), ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(9))), (slipped.Text("baselineStartDate"), slipped.Text("baselineFinishDate")));
        Assert.Equal((2, 6), (slipped.Days("startVarianceDays"), slipped.Days("finishVarianceDays")));
        JsonObject health = await HealthAsync(client, sessions, projectId);
        Assert.Equal(("AMBER", 6, baseline.ToString()), (health.Text("scheduleHealth"), health.Days("finishVarianceDays"), health.Text("projectBaselineId")));

        // The working plan moves on (a rebaseline in preparation); the planned dates change, the reference does not, and the
        // forecast is the planner's, so the variance stays what it was.
        using HttpResponseMessage current = await client.GetAsync($"{ScheduleDriver.Activities}/{works}", sessions.ProjectManager);
        using (HttpResponseMessage replanned = await client.PutAsync($"{ScheduleDriver.Activities}/{works}", sessions.ProjectManager,
                   new { wbsCode = "1", name = new { text = "Works", language = "en" }, requestedStartDate = ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(30)), plannedDurationDays = 10 },
                   AdministrationApi.ETagOf(current)))
        {
            JsonObject moved = await replanned.ReadObjectAsync();
            Assert.Equal(HttpStatusCode.OK, replanned.StatusCode);
            Assert.Equal(ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(30)), moved.Text("plannedStartDate"));
            Assert.Equal((ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(2)), ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(15))),
                (moved.Text("forecastStartDate"), moved.Text("forecastFinishDate")));
            Assert.Equal((ScheduleDriver.Iso(ScheduleDriver.Day1), 2, 6), (moved.Text("baselineStartDate"), moved.Days("startVarianceDays"), moved.Days("finishVarianceDays")));
        }

        // Late by the RED threshold.
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Activities}/{works}/reforecast", Forecast(2, 9 + ScheduleTestHost.RedDays));
        Assert.Equal(("RED", ScheduleTestHost.RedDays), ((await HealthAsync(client, sessions, projectId)).Text("scheduleHealth"), (await HealthAsync(client, sessions, projectId)).Days("finishVarianceDays")));

        // A summary's forecast is rolled up, never entered.
        Guid phase = await client.ActivityAsync(sessions.ProjectManager, projectId, "2", 0, 1);
        await client.ActivityAsync(sessions.ProjectManager, projectId, "2.1", 0, 3, phase);
        using HttpResponseMessage summary = await client.PostAsync($"{ScheduleDriver.Activities}/{phase}/reforecast", sessions.ProjectManager, Forecast(0, 1));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_FORECAST_INVALID"), await summary.RefusalAsync());
    }

    /// <summary>The forecast is no part of a baseline (BR-SCH-031), so it stays open while a candidate is with WF-11.</summary>
    [Fact]
    public async Task TheForecastStaysOpenWhileTheNextCandidateIsWithWf11()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        (Guid works, _) = await host.ApprovedBaselineAsync(client, sessions.ProjectManager, projectId);
        await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId, await host.Database.IssueRebaselineAsync(projectId));

        JsonObject reforecast = await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Activities}/{works}/reforecast", Forecast(1, 12));

        Assert.Equal(3, reforecast.Days("finishVarianceDays"));
    }

    private static object Forecast(int startOffset, int finishOffset) => new
    {
        forecastStartDate = ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(startOffset)),
        forecastFinishDate = ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(finishOffset)),
    };

    private static async Task<JsonObject> HealthAsync(HttpClient client, ScheduleSessions sessions, Guid projectId) =>
        (await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.HealthStatuses, projectId)).Single()!.AsObject();
}
