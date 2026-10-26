using System.Net;
using System.Text.Json.Nodes;
using Npgsql;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>
/// The working schedule: a hierarchy whose summaries roll up, dependencies the backend enforces when it calculates the dates,
/// and the workbook's validation check — a circular dependency between activities is rejected at save time.
/// </summary>
[Collection(ScheduleSuite.Name)]
public sealed class ScheduleBuildTests(ScheduleTestHost host)
{
    /// <summary>
    /// The backend calculates every date (DCL-SCH-03, DCL-SCH-05): a leaf from its requested start and duration, pushed by its
    /// FS predecessor and the lag; the phase spans its children. A client that sends planned or forecast dates is ignored.
    /// </summary>
    [Fact]
    public async Task TheBackendCalculatesTheDatesFromTheInputsAndTheDependencies()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);

        Guid phase = await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 1);
        Guid design = await client.ActivityAsync(sessions.ProjectManager, projectId, "1.1", 0, 10, phase);
        JsonObject build = await client.CreatedOrFailAsync(sessions.ProjectManager, ScheduleDriver.Activities, new
        {
            projectId,
            parentActivityId = phase,
            wbsCode = "1.2",
            name = new { text = "Build", language = "en" },
            requestedStartDate = ScheduleDriver.Iso(ScheduleDriver.Day1),
            plannedDurationDays = 5,
            plannedStartDate = "2020-01-01",
            forecastFinishDate = "2020-01-01",
        });
        Assert.Equal(ScheduleDriver.Iso(ScheduleDriver.Day1), build.Text("plannedStartDate"));

        await client.LinkOrFailAsync(sessions.ProjectManager, design, AdministrationApi.IdOf(build), "FS", lag: 2);

        Dictionary<string, JsonObject> activities = await client.ActivitiesAsync(sessions.ProjectManager, projectId);
        Assert.Equal(("SUMMARY", "ACTIVITY", "ACTIVITY"), (activities["1"].Text("activityKind"), activities["1.1"].Text("activityKind"), activities["1.2"].Text("activityKind")));
        Assert.Equal((ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(12)), ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(16))),
            (activities["1.2"].Text("plannedStartDate"), activities["1.2"].Text("plannedFinishDate")));
        Assert.Equal((ScheduleDriver.Iso(ScheduleDriver.Day1), ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(16)), 17),
            (activities["1"].Text("plannedStartDate"), activities["1"].Text("plannedFinishDate"), activities["1"].Days("plannedDurationDays")));

        // No baseline yet: the forecast follows the plan, and there is nothing to measure a variance against.
        Assert.Equal(activities["1.2"].Text("plannedFinishDate"), activities["1.2"].Text("forecastFinishDate"));
        Assert.Null(activities["1.2"]["finishVarianceDays"]);
    }

    /// <summary>
    /// The workbook's validation check: A → B → C, and C → A is refused when it is saved (422 SCHEDULE_DEPENDENCY_CIRCULAR) and
    /// written nowhere; so is a dependency on itself. The database refuses the cycle too, whoever writes it.
    /// </summary>
    [Fact]
    public async Task ACircularDependencyIsRejectedAtSaveTime()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid a = await client.ActivityAsync(sessions.ProjectManager, projectId, "A", 0, 3);
        Guid b = await client.ActivityAsync(sessions.ProjectManager, projectId, "B", 0, 3);
        Guid c = await client.ActivityAsync(sessions.ProjectManager, projectId, "C", 0, 3);
        await client.LinkOrFailAsync(sessions.ProjectManager, a, b);
        await client.LinkOrFailAsync(sessions.ProjectManager, b, c);

        using (HttpResponseMessage cycle = await client.LinkAsync(sessions.ProjectManager, c, a))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_DEPENDENCY_CIRCULAR"), await cycle.RefusalAsync());
        }

        using (HttpResponseMessage self = await client.LinkAsync(sessions.ProjectManager, a, a))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_DEPENDENCY_INVALID"), await self.RefusalAsync());
        }

        Assert.Equal(2, (await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.Dependencies, projectId)).Count);

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync($"""
            INSERT INTO schedule.schedule_dependency (id, predecessor_activity_id, successor_activity_id, dependency_type, lag_days, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{c}', '{a}', 'FS', 0, now(), '{ScheduleDriver.Person(8)}', now(), '{ScheduleDriver.Person(8)}')
            """));
        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_schedule_dependency_acyclic"), (refused.SqlState, refused.ConstraintName));
    }

    /// <summary>
    /// Two requests that would each close half of a cycle, sent at once: the schedule lock makes the second check see the
    /// first's dependency, so exactly one is saved and the other is refused as circular.
    /// </summary>
    [Fact]
    public async Task TwoConcurrentDependenciesCannotCloseACycleBetweenThem()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid a = await client.ActivityAsync(sessions.ProjectManager, projectId, "A", 0, 3);
        Guid b = await client.ActivityAsync(sessions.ProjectManager, projectId, "B", 0, 3);

        HttpResponseMessage[] responses = await Task.WhenAll(client.LinkAsync(sessions.ProjectManager, a, b), client.LinkAsync(sessions.ProjectManager, b, a));
        try
        {
            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity], responses.Select(r => r.StatusCode).Order());
            Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.Dependencies, projectId));
        }
        finally
        {
            Array.ForEach(responses, r => r.Dispose());
        }
    }

    /// <summary>
    /// VAL-SCH-002/003, BR-SCH-026: no activity is its own ancestor; a dependency end is no parent and a summary no dependency
    /// end; a WBS code is unique in the schedule.
    /// </summary>
    [Fact]
    public async Task TheHierarchyStaysAcyclicAndDependenciesJoinLeavesOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid phase = await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 1);
        Guid child = await client.ActivityAsync(sessions.ProjectManager, projectId, "1.1", 0, 4, phase);
        Guid other = await client.ActivityAsync(sessions.ProjectManager, projectId, "2", 0, 4);
        await client.LinkOrFailAsync(sessions.ProjectManager, child, other);

        using HttpResponseMessage current = await client.GetAsync($"{ScheduleDriver.Activities}/{phase}", sessions.ProjectManager);
        using (HttpResponseMessage underItsChild = await client.PutAsync($"{ScheduleDriver.Activities}/{phase}", sessions.ProjectManager,
                   new { parentActivityId = child, wbsCode = "1", name = new { text = "Phase", language = "en" }, requestedStartDate = ScheduleDriver.Iso(ScheduleDriver.Day1), plannedDurationDays = 1 },
                   AdministrationApi.ETagOf(current)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_HIERARCHY_CIRCULAR"), await underItsChild.RefusalAsync());
        }

        using (HttpResponseMessage underADependencyEnd = await client.PostAsync(ScheduleDriver.Activities, sessions.ProjectManager, ScheduleDriver.Activity(projectId, "2.1", 0, 2, other)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_HIERARCHY_INVALID"), await underADependencyEnd.RefusalAsync());
        }

        using (HttpResponseMessage ontoASummary = await client.LinkAsync(sessions.ProjectManager, other, phase))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_DEPENDENCY_INVALID"), await ontoASummary.RefusalAsync());
        }

        using HttpResponseMessage sameCode = await client.PostAsync(ScheduleDriver.Activities, sessions.ProjectManager, ScheduleDriver.Activity(projectId, "1.1", 0, 2));
        Assert.Equal((HttpStatusCode.Conflict, "SCHEDULE_WBS_CODE_EXISTS"), await sameCode.RefusalAsync());
    }

    /// <summary>RETAIN: an activity is cancelled, never deleted, and only once nothing hangs on it; it then leaves the plan.</summary>
    [Fact]
    public async Task ACancelledActivityLeavesThePlanOnceNothingDependsOnIt()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid phase = await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 1);
        Guid early = await client.ActivityAsync(sessions.ProjectManager, projectId, "1.1", 0, 4, phase);
        Guid late = await client.ActivityAsync(sessions.ProjectManager, projectId, "1.2", 20, 4, phase);
        Guid dependency = await client.LinkOrFailAsync(sessions.ProjectManager, early, late);

        using (HttpResponseMessage inUse = await client.PostAsync($"{ScheduleDriver.Activities}/{late}/cancel", sessions.ProjectManager))
        {
            Assert.Equal((HttpStatusCode.Conflict, "SCHEDULE_ACTIVITY_IN_USE"), await inUse.RefusalAsync());
        }

        using (HttpResponseMessage removed = await client.SendAsync(HttpMethod.Delete, $"{ScheduleDriver.Dependencies}/{dependency}", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        Assert.Equal("CANCELLED", (await client.CommandOrFailAsync(sessions.ProjectManager, $"{ScheduleDriver.Activities}/{late}/cancel")).Text("status"));
        Dictionary<string, JsonObject> activities = await client.ActivitiesAsync(sessions.ProjectManager, projectId);
        Assert.Equal(ScheduleDriver.Iso(ScheduleDriver.Day1.AddDays(3)), activities["1"].Text("plannedFinishDate"));

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync($"DELETE FROM schedule.schedule_activity WHERE id = '{late}'"));
        Assert.Contains("never deleted", refused.MessageText, StringComparison.Ordinal);
    }

    /// <summary>The spec's SCH-CC-03: planning begins once the project is approved, and the schedule is initialized once.</summary>
    [Fact]
    public async Task AScheduleIsPlannedForAnApprovedOrActiveProjectOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid draft = await host.ProjectAsync("DRAFT");
        Guid active = await host.ProjectAsync("ACTIVE");

        using (HttpResponseMessage tooEarly = await client.PostAsync(ScheduleDriver.Schedules, sessions.ProjectManager, new { projectId = draft }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_PROJECT_NOT_ELIGIBLE"), await tooEarly.RefusalAsync());
        }

        using (HttpResponseMessage notInitialized = await client.PostAsync(ScheduleDriver.Activities, sessions.ProjectManager, ScheduleDriver.Activity(active, "1", 0, 2)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "SCHEDULE_NOT_INITIALIZED"), await notInitialized.RefusalAsync());
        }

        await client.CreatedOrFailAsync(sessions.ProjectManager, ScheduleDriver.Schedules, new { projectId = active });
        using (HttpResponseMessage again = await client.PostAsync(ScheduleDriver.Schedules, sessions.ProjectManager, new { projectId = active }))
        {
            Assert.Equal((HttpStatusCode.Conflict, "SCHEDULE_EXISTS"), await again.RefusalAsync());
        }

        JsonObject schedule = (await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.Schedules, active)).Single()!.AsObject();
        Assert.Null(schedule["activeBaselineId"]);
        Assert.Equal("UNKNOWN", (await client.ItemsAsync(sessions.ProjectManager, ScheduleDriver.HealthStatuses, active)).Single()!.Text("scheduleHealth"));
    }
}
