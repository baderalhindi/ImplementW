using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Execution;

/// <summary>
/// Separation 4 (schedule-baseline.md D-8, D-9, D-17; erd.md F-015, F-017): variance and Schedule Health are measured against the
/// one ACTIVE Approved Baseline, and every reader names the same one. A rebaseline candidate — drafted, with WF-11, or returned —
/// is never the reference; on approval it becomes the reference everywhere at once.
/// </summary>
[Collection(ScheduleSuite.Name)]
public sealed class ApprovedBaselineReferenceTests(ScheduleTestHost host)
{
    [Fact]
    public async Task EveryReaderMeasuresAgainstTheActiveApprovedBaselineAndOnlyIt()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        (Guid works, Guid first) = await host.ApprovedBaselineAsync(client, sessions.ProjectManager, projectId);

        // The forecast slips 6 days past baseline 1: AMBER.
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Activities}/{works}/reforecast", Forecast(2, 15));
        await AssertReferenceAsync(client, sessions, projectId, first, "AMBER", 6, "after the slip");

        // The plan is redrawn around the slip and a rebaseline is drafted: the working plan and the draft are no reference.
        await ReplanAsync(client, sessions, works, startOffset: 2, days: 14);
        Guid second = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, ScheduleDriver.Baselines, new { projectId }));
        await AssertReferenceAsync(client, sessions, projectId, first, "AMBER", 6, "with the rebaseline drafted");

        // With WF-11, and then returned by it, the candidate is still no reference, whatever the schedule does meanwhile.
        Guid authorization = host.Rebaselines.Authorize(projectId);
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Baselines}/{second}/submit", new { changeAuthorizationId = authorization });
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Activities}/{works}/reforecast", Forecast(2, 15));
        await AssertReferenceAsync(client, sessions, projectId, first, "AMBER", 6, "with the rebaseline before WF-11");
        await host.DecideAndDeliverAsync(second, ApprovalTaskDecision.Return, "Justify the new finish");
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Activities}/{works}/reforecast", Forecast(2, 15));
        await AssertReferenceAsync(client, sessions, projectId, first, "AMBER", 6, "with the rebaseline returned");
        Assert.Equal(["1 ACTIVE", "2 RETURNED"], await host.BaselineStatesAsync(projectId));

        // Resubmitted and approved: baseline 2 is the reference for every reader at once, and the slip it absorbed is gone.
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Baselines}/{second}/submit", new { changeAuthorizationId = authorization });
        await host.DecideAndDeliverAsync(second, ApprovalTaskDecision.Approve);
        Assert.Equal(["1 SUPERSEDED", "2 ACTIVE"], await host.BaselineStatesAsync(projectId));
        await AssertReferenceAsync(client, sessions, projectId, second, "GREEN", 0, "after the rebaseline was approved");

        // The next slip is measured from baseline 2 alone.
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Activities}/{works}/reforecast", Forecast(2, 15 + ScheduleTestHost.RedDays));
        await AssertReferenceAsync(client, sessions, projectId, second, "RED", ScheduleTestHost.RedDays, "after a slip past baseline 2");
    }

    /// <summary>
    /// The baseline every reader names is <paramref name="baselineId"/>, and it is the project's one ACTIVE baseline: the health API,
    /// the read side's port (edge 29), the stored row, and the activity's variance, which agrees with the project's.
    /// </summary>
    private async Task AssertReferenceAsync(HttpClient client, ScheduleSessions sessions, Guid projectId, Guid baselineId, string health, int lateDays, string when)
    {
        string expected = baselineId.ToString();
        Assert.Equal([expected], await host.Database.QueryAsync($"SELECT id::text FROM schedule.project_baseline WHERE project_id = '{projectId}' AND status = 'ACTIVE'"));

        JsonObject served = Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.HealthStatuses, projectId))!.AsObject();
        Assert.True((served.Text("projectBaselineId"), served.Text("scheduleHealth"), served.Days("finishVarianceDays")) == (expected, health, lateDays),
            $"{when}: the health API measured against {served.Text("projectBaselineId")}, {served.Text("scheduleHealth")} {served.Days("finishVarianceDays")}; expected {expected}, {health} {lateDays}");

        ScheduleHealthStatusDetail? port = await host.WithScopeAsync(services => services.GetRequiredService<IScheduleHealthReader>().GetAsync(projectId, CancellationToken.None));
        Assert.Equal((baselineId, lateDays), (port?.ProjectBaselineId, port?.FinishVarianceDays));
        Assert.Equal([$"{expected} {lateDays}"], await host.Database.QueryAsync(
            $"SELECT project_baseline_id || ' ' || finish_variance_days FROM schedule.schedule_health_status WHERE project_id = '{projectId}'"));

        JsonObject activity = (await client.ActivitiesAsync(sessions.ProjectManager, projectId))["1"];
        Assert.Equal((expected, lateDays), (activity.Text("baselineId"), activity.Days("finishVarianceDays")));
    }

    private static async Task ReplanAsync(HttpClient client, ScheduleSessions sessions, Guid activityId, int startOffset, int days)
    {
        using HttpResponseMessage current = await client.GetAsync($"{ScheduleDriver.Activities}/{activityId}", sessions.ProjectManager);
        using HttpResponseMessage replanned = await client.PutAsync($"{ScheduleDriver.Activities}/{activityId}", sessions.ProjectManager,
            new { wbsCode = "1", name = new { text = "Works", language = "en" }, requestedStartDate = ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(startOffset)), plannedDurationDays = days },
            AdministrationApi.ETagOf(current));
        Assert.Equal(HttpStatusCode.OK, replanned.StatusCode);
    }

    private static object Forecast(int startOffset, int finishOffset) => new
    {
        forecastStartDate = ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(startOffset)),
        forecastFinishDate = ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(finishOffset)),
    };
}
