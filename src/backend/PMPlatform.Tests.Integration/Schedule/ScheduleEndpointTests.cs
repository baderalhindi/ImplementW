using System.Net;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>The WF-03 surface's gates and shapes: 401, 403, R-47's 404, 400 shape errors, R-21's If-Match, R-3's empty collection.</summary>
[Collection(ScheduleSuite.Name)]
public sealed class ScheduleEndpointTests(ScheduleTestHost host)
{
    [Fact]
    public async Task EveryOperationNeedsASessionAndThePermission()
    {
        using HttpClient client = host.Api.CreateClient();
        Guid projectId = await host.ProjectAsync();
        string viewer = (await client.SignInOrFailAsync(6)).AccessToken;

        using (HttpResponseMessage anonymous = await client.GetAsync($"{ScheduleDriver.Activities}?projectId={projectId}"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        foreach (string collection in new[] { ScheduleDriver.Schedules, ScheduleDriver.Activities, ScheduleDriver.Dependencies, ScheduleDriver.Baselines, ScheduleDriver.HealthStatuses })
        {
            using HttpResponseMessage refused = await client.GetAsync($"{collection}?projectId={projectId}", viewer);
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{collection}: {(int)refused.StatusCode}");
        }
    }

    /// <summary>
    /// ADR-013 and R-47: an entity Project Manager reaches the projects they manage only. Another manager's project is an empty
    /// collection and a 404, exactly as if it did not exist.
    /// </summary>
    [Fact]
    public async Task AProjectManagerReachesOnlyTheProjectsTheyManage()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid theirs = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid theirActivity = await client.ActivityAsync(sessions.ProjectManager, theirs, "1", 0, 5);
        Guid someoneElses = await host.ProjectAsync(projectManager: 5);
        await client.CreatedOrFailAsync(sessions.Portfolio, ScheduleDriver.Schedules, new { projectId = someoneElses });
        Guid otherActivity = await client.ActivityAsync(sessions.Portfolio, someoneElses, "1", 0, 5);

        Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.Activities, theirs));
        Assert.Empty(await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.Activities, someoneElses));
        using (HttpResponseMessage hidden = await client.GetAsync($"{ScheduleDriver.Activities}/{otherActivity}", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        using (HttpResponseMessage notTheirs = await client.PostAsync(ScheduleDriver.Activities, sessions.ProjectManager, ScheduleDriver.Activity(someoneElses, "2", 0, 5)))
        {
            Assert.Equal(HttpStatusCode.NotFound, notTheirs.StatusCode);
        }

        using HttpResponseMessage across = await client.LinkAsync(sessions.ProjectManager, otherActivity, theirActivity);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_DEPENDENCY_INVALID"), await across.RefusalAsync());
        Assert.Equal(["predecessorActivityId NOT_FOUND"], await across.ReadFieldErrorsAsync());
    }

    [Fact]
    public async Task ARequestIsShapeCheckedBeforeAnythingIsWritten()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid activity = await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 5);

        using (HttpResponseMessage noProject = await client.GetAsync(ScheduleDriver.Activities, sessions.ProjectManager))
        {
            Assert.Equal(["projectId REQUIRED"], await noProject.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage badActivity = await client.PostAsync(ScheduleDriver.Activities, sessions.ProjectManager, new { projectId, wbsCode = "2", plannedDurationDays = 0, sortOrder = -1 }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, badActivity.StatusCode);
            Assert.Equal(["name REQUIRED", "requestedStartDate REQUIRED", "plannedDurationDays OUT_OF_RANGE", "sortOrder OUT_OF_RANGE"], await badActivity.ReadFieldErrorsAsync());
        }

        // Start-to-finish is deferred in the MVP (BR-SCH-024), and a lead is no lag (BR-SCH-025).
        using (HttpResponseMessage badLink = await client.LinkAsync(sessions.ProjectManager, activity, activity, "SF", -1))
        {
            Assert.Equal(["dependencyType ENUM_VALUE", "lagDays OUT_OF_RANGE"], await badLink.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage backwards = await client.PostAsync($"{ScheduleDriver.Activities}/{activity}/reforecast", sessions.ProjectManager,
                   new { forecastStartDate = ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(5)), forecastFinishDate = ScheduleDriver.Iso(ScheduleDriver.Day1) }))
        {
            Assert.Equal(["forecastFinishDate DATE_BEFORE_START"], await backwards.ReadFieldErrorsAsync());
        }

        using HttpResponseMessage noIfMatch = await client.PutAsync($"{ScheduleDriver.Activities}/{activity}", sessions.ProjectManager,
            new { wbsCode = "1", name = new { text = "Works", language = "en" }, requestedStartDate = ScheduleDriver.Iso(ScheduleDriver.Day1), plannedDurationDays = 5 }, null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, noIfMatch.StatusCode);
    }
}
