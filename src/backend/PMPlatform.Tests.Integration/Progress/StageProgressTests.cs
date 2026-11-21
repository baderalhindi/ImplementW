using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Project;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>
/// Review round 7, item 1: stage-weighted progress. With project stages configured a period is stage-based: overall progress is Σ w·c ÷ Σ w over the
/// stages that apply, a project with no schedule reports, the schedule measures Execution only, a decrease needs a reason, and publishing writes the
/// figures and the current stage to the project, where they replace the declared ones.
/// </summary>
[Collection(StageProgressSuite.Name)]
public sealed class StageProgressTests(ProgressTestHost host) : IAsyncLifetime
{
    public Task InitializeAsync() => StageFixture.PublishAsync(host.Database);

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>The client's acceptance figures: 100/100/100/40/0/0 is 52.0%, and 43.5% when design does not apply; the project needs no schedule.</summary>
    [Theory]
    [InlineData(null, 52.0)]
    [InlineData("DESIGN", 43.5)]
    public async Task OverallIsTheWeightedSumOfTheApplicableStagesAndNeedsNoSchedule(string? skipped, double expected)
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await PlanAsync(projectId, skipped);

        JsonObject draft = await client.StartOrFailAsync(sessions.ProjectManager, projectId);
        Guid id = AdministrationApi.IdOf(draft);
        Assert.Equal(("STAGE_WEIGHTED", StageFixture.VersionId), (draft["actualSource"]!.GetValue<string>(), Guid.Parse(draft["stageConfigurationVersionId"]!.GetValue<string>())));
        Assert.Equal(StageFixture.Stages.Select(s => s.Code), draft["stages"]!.AsArray().Select(s => s!["stageCode"]!.GetValue<string>()));

        using (HttpResponseMessage edited = await client.EditAsync(sessions.ProjectManager, id, Entries(skipped, 100, 100, 100, 40, 0, 0)))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        JsonObject submitted = await client.CommandOrFailAsync(sessions.ProjectManager, id, "submit");
        Assert.Equal((decimal)expected, decimal.Round(submitted.Percent("actualPercent"), 1));
        Assert.Equal("EXECUTION", submitted["currentStageCode"]!.GetValue<string>());

        await client.CommandOrFailAsync(sessions.Reviewer, id, "start-review");
        JsonObject published = await client.CommandOrFailAsync(sessions.Reviewer, id, "publish");
        JsonObject project = JsonNode.Parse(Assert.Single(await host.Database.QueryAsync(
            $"SELECT to_jsonb(p)::text FROM project.project p WHERE p.id = '{projectId}'")))!.AsObject();
        Assert.Equal((decimal)expected, decimal.Round(project["declared_actual_percent"]!.GetValue<decimal>(), 1));
        Assert.Equal(published.Percent("plannedPercent"), project["declared_planned_percent"]!.GetValue<decimal>());
        Assert.Equal(("EXECUTION", 40m, "PROGRESS_UPDATE"), (
            project["current_stage_code"]!.GetValue<string>(), project["current_stage_percent"]!.GetValue<decimal>(), project["declared_source"]!.GetValue<string>()));
    }

    [Fact]
    public async Task AStageThatStartedNeedsItsStartAndOneThatFinishedItsFinish()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await PlanAsync(projectId, null);
        Guid id = AdministrationApi.IdOf(await client.StartOrFailAsync(sessions.ProjectManager, projectId));

        using (HttpResponseMessage edited = await client.EditAsync(sessions.ProjectManager, id, new
        {
            narrative = (object?)null,
            @override = (object?)null,
            stages = new object[]
            {
                new { stageCode = "BUSINESS_CASE", percentComplete = 100m },
                new { stageCode = "DESIGN", percentComplete = 20m },
                new { stageCode = "TENDERING", percentComplete = 0m },
                new { stageCode = "EXECUTION", percentComplete = 0m },
                new { stageCode = "TESTING", percentComplete = 0m },
                new { stageCode = "HANDOVER", percentComplete = 0m },
            },
        }))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        using HttpResponseMessage refused = await client.CommandAsync(sessions.ProjectManager, id, "submit");
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROGRESS_STAGE_ENTRY_INVALID"), await refused.RefusalAsync());
        Assert.Equal(
            ["stages[0].actualStartDate REQUIRED", "stages[0].actualFinishDate REQUIRED", "stages[1].actualStartDate REQUIRED"],
            await refused.ReadFieldErrorsAsync());
    }

    /// <summary>With an active baseline the schedule measures Execution: its % is the roll-up and cannot be typed.</summary>
    [Fact]
    public async Task WithABaselineExecutionIsTheScheduleAndCannotBeTyped()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await PlanAsync(projectId, null);
        host.Inputs.Set(projectId, 30m, ProgressDriver.Today.AddDays(-10));

        JsonObject draft = await client.StartOrFailAsync(sessions.ProjectManager, projectId);
        JsonNode execution = draft["stages"]!.AsArray().Single(s => s!["stageCode"]!.GetValue<string>() == "EXECUTION")!;
        Assert.Equal((true, 30m), (execution["isFromSchedule"]!.GetValue<bool>(), execution.Percent("percentComplete")));

        using HttpResponseMessage typed = await client.EditAsync(sessions.ProjectManager, AdministrationApi.IdOf(draft), Entries(null, 100, 100, 100, 50, 0, 0));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROGRESS_STAGE_ENTRY_INVALID"), await typed.RefusalAsync());
        Assert.Equal(["stages[3].percentComplete NOT_ALLOWED"], await typed.ReadFieldErrorsAsync());
    }

    /// <summary>A % lower than the last published period's needs a reason; the next period starts from the last one's figures.</summary>
    [Fact]
    public async Task ADecreaseNeedsAReasonAndTheNextPeriodStartsFromTheLast()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(activatedDaysAgo: 20);
        await PlanAsync(projectId, null);
        await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, projectId, Entries(null, 100, 100, 100, 40, 0, 0));

        JsonObject next = await client.StartOrFailAsync(sessions.ProjectManager, projectId);
        Assert.Equal(
            [100m, 100m, 100m, 40m, 0m, 0m],
            next["stages"]!.AsArray().Select(s => s!.Percent("percentComplete")));
        Assert.Equal(40m, next["stages"]![3]!.Percent("previousPercent"));
        Guid id = AdministrationApi.IdOf(next);

        using (HttpResponseMessage edited = await client.EditAsync(sessions.ProjectManager, id, Entries(null, 100, 100, 100, 35, 0, 0)))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        using (HttpResponseMessage refused = await client.CommandAsync(sessions.ProjectManager, id, "submit"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROGRESS_STAGE_ENTRY_INVALID"), await refused.RefusalAsync());
            Assert.Equal(["stages[3].decreaseReason REQUIRED"], await refused.ReadFieldErrorsAsync());
        }

        using (HttpResponseMessage explained = await client.EditAsync(
                   sessions.ProjectManager, id, Entries(null, 100, 100, 100, 35, 0, 0, decreaseReason: "Rework on the foundations")))
        {
            Assert.Equal(HttpStatusCode.OK, explained.StatusCode);
        }

        Assert.Equal("SUBMITTED", (await client.CommandOrFailAsync(sessions.ProjectManager, id, "submit")).Status());
    }

    /// <summary>
    /// Review round 8, point 02: the project's one overall figure follows its method, the same on its Overview and in the register; a change of method
    /// after an approved update needs a reason and is in its history; the schedule method then reads the schedule, which this project has none of.
    /// </summary>
    [Fact]
    public async Task TheOverallFigureFollowsTheMethodAndAChangeAfterAnApprovedUpdateIsExplained()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        await PlanAsync(projectId, null);
        string path = $"/api/v1/projects/{projectId}";

        async Task<HttpResponseMessage> SetMethodAsync(string method, string? reason)
        {
            using HttpResponseMessage current = await client.GetAsync(path, sessions.ProjectManager);
            return await client.PostAsync($"{path}/set-progress-method", sessions.ProjectManager,
                new { progressMethod = method, reason = reason is null ? null : new { text = reason, language = "en" } }, AdministrationApi.ETagOf(current));
        }

        async Task<JsonNode> ProgressAsync()
        {
            using HttpResponseMessage detail = await client.GetAsync(path, sessions.ProjectManager);
            return (await detail.ReadObjectAsync())["indicators"]!["progress"]!;
        }

        // The project came in without a method: the stage-plan writer sets it, with no reason before any approved update.
        Assert.Null((await ProgressAsync())["method"]?.GetValue<string>());
        using (HttpResponseMessage set = await SetMethodAsync("STAGES", null))
        {
            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        }

        await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, projectId, Entries(null, 100, 100, 100, 40, 0, 0));

        // 5 + 15 + 10 + 55 × 40% = 52; planned today: the first three over, execution half way, so 5 + 15 + 10 + 55 × 50% = 57.5.
        JsonNode progress = await ProgressAsync();
        Assert.Equal(("STAGES", "STAGES", "EXECUTION"), (progress["method"]!.GetValue<string>(), progress["source"]!.GetValue<string>(), progress["currentStageCode"]!.GetValue<string>()));
        Assert.Equal((52m, 57.5m), (progress.Percent("actualPercent"), decimal.Round(progress.Percent("plannedPercent"), 1)));
        Assert.Equal(6, progress["stages"]!.AsArray().Count);

        using (HttpResponseMessage register = await client.GetAsync("/api/v1/projects?pageSize=100", sessions.ProjectManager))
        {
            JsonNode row = (await register.ReadObjectAsync())["items"]!.AsArray().Single(i => i!["id"]!.GetValue<string>() == projectId.ToString())!;
            Assert.Equal(52m, row["indicators"]!["progress"]!.Percent("actualPercent"));
            Assert.Equal(-5.5m, decimal.Round(row["indicators"]!.Percent("gapPoints"), 1));
        }

        using (HttpResponseMessage unexplained = await SetMethodAsync("SCHEDULE", null))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROJECT_PROGRESS_METHOD_REASON_REQUIRED"), await unexplained.RefusalAsync());
        }

        using (HttpResponseMessage explained = await SetMethodAsync("SCHEDULE", "The work breakdown is now baselined"))
        {
            Assert.Equal(HttpStatusCode.OK, explained.StatusCode);
        }

        JsonNode scheduled = await ProgressAsync();
        Assert.Equal(("SCHEDULE", "SCHEDULE"), (scheduled["method"]!.GetValue<string>(), scheduled["source"]!.GetValue<string>()));
        Assert.Null(scheduled["actualPercent"]?.GetValue<decimal>());
        Assert.Equal(2, (await host.Database.QueryAsync(
            $"SELECT event_type FROM audit_activity.audit_event WHERE event_type = 'Project.ProgressMethodChanged' AND subject_id = '{projectId}'")).Count);
    }

    /// <summary>With stages configured and no stage plan the update asks for the plan first.</summary>
    [Fact]
    public async Task WithoutAStagePlanTheUpdateAsksForItFirst()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();

        using HttpResponseMessage refused = await client.StartAsync(sessions.ProjectManager, projectId);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROGRESS_STAGE_PLAN_REQUIRED"), await refused.RefusalAsync());
    }

    /// <summary>
    /// Every stage applies but <paramref name="skipped"/>; business case, design and tendering are over, execution runs from 100 days ago to 100 days
    /// ahead, testing and handover are to come.
    /// </summary>
    private Task PlanAsync(Guid projectId, string? skipped)
    {
        DateOnly today = ProgressDriver.Today;
        string Day(int offset) => $"'{today.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}'";
        (string Code, int From, int To)[] dates =
            [("BUSINESS_CASE", -400, -300), ("DESIGN", -300, -200), ("TENDERING", -200, -100), ("EXECUTION", -100, 100), ("TESTING", 100, 150), ("HANDOVER", 150, 200)];
        string rows = string.Join(", ", dates.Select(d => d.Code == skipped
            ? $"(gen_random_uuid(), '{projectId}', '{d.Code}', false, NULL, NULL, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')"
            : $"(gen_random_uuid(), '{projectId}', '{d.Code}', true, {Day(d.From)}, {Day(d.To)}, now(), '{IdentityDatabase.SeedPrincipalId}', now(), '{IdentityDatabase.SeedPrincipalId}')"));
        return host.Database.ExecuteAsync($"""
            INSERT INTO project.project_stage_plan (id, project_id, stage_code, applies, planned_start_date, planned_end_date, created_at, created_by, updated_at, updated_by)
            VALUES {rows};
            UPDATE project.project SET stage_plan_revision_no = 1 WHERE id = '{projectId}';
            """);
    }

    /// <summary>The six stages' % in stage order, a stage that started a year ago, a complete one finished a month ago.</summary>
    private static object Entries(string? skipped, decimal businessCase, decimal design, decimal tendering, decimal execution, decimal testing, decimal handover, string? decreaseReason = null)
    {
        DateOnly today = ProgressDriver.Today;
        decimal[] percents = [businessCase, design, tendering, execution, testing, handover];
        return new
        {
            narrative = (object?)null,
            @override = (object?)null,
            stages = StageFixture.Stages.Select((s, i) => s.Code == skipped
                ? (object)new { stageCode = s.Code }
                : new
                {
                    stageCode = s.Code,
                    percentComplete = percents[i],
                    actualStartDate = percents[i] > 0 ? today.AddDays(-365 + (i * 30)) : (DateOnly?)null,
                    actualFinishDate = percents[i] == 100 ? today.AddDays(-30) : (DateOnly?)null,
                    decreaseReason = s.Code == "EXECUTION" && decreaseReason is not null ? new { text = decreaseReason, language = "en" } : null,
                }).ToArray(),
        };
    }
}

/// <summary>A database of its own: with stages published, every period in it is stage-based.</summary>
[CollectionDefinition(Name)]
public sealed class StageProgressSuite : ICollectionFixture<ProgressTestHost>
{
    public const string Name = "StageProgress";
}
