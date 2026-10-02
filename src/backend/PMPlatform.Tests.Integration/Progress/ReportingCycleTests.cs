using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Application.Features.Progress;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>
/// Reporting periods from the governance profile's cadence, reported one at a time, earliest first; ADR-014's opening
/// position, entered once and governed like any other period.
/// </summary>
[Collection(ProgressSuite.Name)]
public sealed class ReportingCycleTests(ProgressTestHost host)
{
    [Fact]
    public async Task PeriodsAreGeneratedFromTheProfileCadenceAndReportedEarliestFirst()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(activatedDaysAgo: 20);
        host.Inputs.Set(projectId, actualPercent: 20, baselineStart: ProgressDriver.Today.AddDays(-49));

        Assert.Empty(await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Cycles, projectId));
        JsonObject first = await client.StartOrFailAsync(sessions.ProjectManager, projectId);

        DateOnly activated = ProgressDriver.Today.AddDays(-20);
        JsonArray cycles = await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Cycles, projectId);
        Assert.Equal(
            [
                (activated, activated.AddDays(6), "OPEN"),
                (activated.AddDays(7), activated.AddDays(13), "OPEN"),
                (activated.AddDays(14), activated.AddDays(20), "OPEN"),
            ],
            cycles.Select(c => (Date(c!, "periodStart"), Date(c!, "periodEnd"), c!.Status())));
        Assert.All(cycles, c => Assert.Equal(Date(c!, "periodEnd"), Date(c!, "dueDate")));
        Assert.Equal(cycles[0]!["id"]!.GetValue<string>(), first["reportingCycleId"]!.GetValue<string>());

        using (HttpResponseMessage second = await client.StartAsync(sessions.ProjectManager, projectId))
        {
            Assert.Equal((HttpStatusCode.Conflict, "PROGRESS_SUBMISSION_EXISTS"), await second.RefusalAsync());
        }

        Guid id = AdministrationApi.IdOf(first);
        await client.CommandOrFailAsync(sessions.ProjectManager, id, "submit");
        await client.CommandOrFailAsync(sessions.Reviewer, id, "start-review");
        await client.CommandOrFailAsync(sessions.Reviewer, id, "publish");

        JsonObject next = await client.StartOrFailAsync(sessions.ProjectManager, projectId);
        cycles = await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Cycles, projectId);
        Assert.Equal(["CLOSED", "OPEN", "OPEN"], cycles.Select(c => c!.Status()));
        Assert.Equal(cycles[1]!["id"]!.GetValue<string>(), next["reportingCycleId"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProgressIsReportedOnAnActiveProjectAndOnlyForPeriodsThatHaveBegun()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid planned = await host.ProjectAsync(state: "APPROVED_PLANNED");
        host.Inputs.Set(planned, actualPercent: 0, baselineStart: ProgressDriver.Today);
        using (HttpResponseMessage notActive = await client.StartAsync(sessions.ProjectManager, planned))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "PROGRESS_PROJECT_NOT_ACTIVE"), await notActive.RefusalAsync());
        }

        Guid activatedToday = await host.ProjectAsync(activatedDaysAgo: 0);
        host.Inputs.Set(activatedToday, actualPercent: 5, baselineStart: ProgressDriver.Today);
        await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, activatedToday);
        using HttpResponseMessage nothingDue = await client.StartAsync(sessions.ProjectManager, activatedToday);
        Assert.Equal((HttpStatusCode.Conflict, "PROGRESS_NOTHING_TO_REPORT"), await nothingDue.RefusalAsync());
    }

    /// <summary>
    /// ADR-014: a project taken in already under way starts from its opening position, entered once at intake and not
    /// reconstructed. It is recorded once whatever the event's redelivery, submitted for review rather than published by
    /// itself, and published through review like any period; regular periods follow from the day after the intake.
    /// </summary>
    [Fact]
    public async Task AnOpeningPositionIsEnteredOnceAndPublishedThroughReview()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        DateOnly intakeDate = ProgressDriver.Today.AddDays(-30);
        Guid projectId = await host.ProjectAsync(activatedDaysAgo: 30, legacyIntakeDate: intakeDate);
        Guid intakeId = Guid.NewGuid();
        await host.Database.ExecuteAsync($"""
            INSERT INTO project.project_intake (id, project_id, intake_date, declared_scope, declared_scope_lang, declared_budget_sar, declared_end_date,
                                               opening_percent_complete, opening_spend_to_date_sar, recorded_by_user_id, created_at, created_by, updated_at, updated_by)
            VALUES ('{intakeId}', '{projectId}', '{intakeDate:yyyy-MM-dd}', 'Road works already under way', 'en', 5000000, '{intakeDate.AddYears(1):yyyy-MM-dd}',
                    35, 1500000, '{ProgressDriver.Person(2)}', now(), '{ProgressDriver.Person(2)}', now(), '{ProgressDriver.Person(2)}')
            """);
        OpeningPosition position = new(projectId, intakeId, intakeDate, 35, ProgressDriver.Person(2));

        Assert.True(await RecordAsync(position));
        Assert.False(await RecordAsync(position));

        JsonNode opening = Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Submissions, projectId))!;
        Assert.Equal(("SUBMITTED", 35m, intakeId.ToString(), ProgressDriver.Person(2).ToString()),
            (opening.Status(), opening.Percent("actualPercentCalculated"), opening["projectIntakeId"]!.GetValue<string>(), opening["submittedByUserId"]!.GetValue<string>()));
        Assert.Null(opening["plannedPercent"]);
        JsonNode openingPeriod = Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Cycles, projectId))!;
        Assert.Equal((intakeDate, intakeDate), (Date(openingPeriod, "periodStart"), Date(openingPeriod, "periodEnd")));

        // The opening period is still being reviewed, so the next one waits.
        host.Inputs.Set(projectId, actualPercent: 40, baselineStart: intakeDate);
        using (HttpResponseMessage waiting = await client.StartAsync(sessions.ProjectManager, projectId))
        {
            Assert.Equal((HttpStatusCode.Conflict, "PROGRESS_SUBMISSION_EXISTS"), await waiting.RefusalAsync());
        }

        Guid openingId = Guid.Parse(opening["id"]!.GetValue<string>());
        await client.CommandOrFailAsync(sessions.Reviewer, openingId, "start-review");
        await client.CommandOrFailAsync(sessions.Reviewer, openingId, "publish");

        // Published as entered; with no plan behind it, its health is UNKNOWN — not coerced to a colour.
        JsonNode snapshot = Assert.Single(await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Snapshots, projectId))!;
        Assert.Equal((35m, "UNKNOWN"), (snapshot.Percent("actualPercent"), snapshot["overallHealth"]!.GetValue<string>()));

        JsonObject regular = await client.StartOrFailAsync(sessions.ProjectManager, projectId);
        JsonArray cycles = await client.ItemsAsync(sessions.ProjectManager, ProgressDriver.Cycles, projectId);
        Assert.Equal(intakeDate.AddDays(1), Date(cycles[1]!, "periodStart"));
        Assert.Equal(cycles[1]!["id"]!.GetValue<string>(), regular["reportingCycleId"]!.GetValue<string>());
        Assert.Equal(40m, regular.Percent("actualPercentCalculated"));
    }

    private async Task<bool> RecordAsync(OpeningPosition position)
    {
        await using AsyncServiceScope scope = host.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProgressOpeningPosition>().RecordAsync(position, CancellationToken.None);
    }

    private static DateOnly Date(JsonNode node, string property) => DateOnly.Parse(node[property]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
}
