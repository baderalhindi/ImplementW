using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Milestone;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Execution;

/// <summary>
/// Separation 2 (ICD-04, milestone-achievement.md D-1, ADR-003 §8.2 edge 10): a milestone is one <c>schedule.project_milestone</c>
/// row under two authorities. WF-03 owns its representation and dates, WF-05 its achievement and the accepted actual date. Nothing
/// in the database stops a second row for the same milestone (it has no natural key), so these follow one milestone through every
/// write either authority makes and count it after each.
/// </summary>
[Collection(MilestoneSuite.Name)]
public sealed class SharedMilestoneTests(MilestoneTestHost host)
{
    /// <summary>The tables that may name a shared milestone: WF-03's baseline copy and WF-05's achievement revisions (D-1, D-11).</summary>
    private static readonly string[] MilestoneReferences = ["milestone.milestone_achievement.project_milestone_id", "schedule.baseline_milestone.project_milestone_id"];

    [Fact]
    public async Task OneMilestoneStaysOneRowThroughEveryWriteOfWf03AndWf05()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await MilestoneDriver.SignInAsync(client);
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid activityId = await client.ActivityAsync(sessions.ProjectManager, projectId, "1", 0, 10);
        DateOnly planned = MilestoneDriver.Today.AddDays(30);

        // WF-03 plans the milestone on its activity.
        Guid milestoneId = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, MilestoneDriver.Milestones,
            MilestoneDriver.Milestone(projectId, forecast: planned, activityId: activityId)));
        await AssertOneRowAsync(projectId, milestoneId, "planned");

        // WF-03 baselines it through WF-11: the baseline copies its date and names the row; it creates none.
        Guid first = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId));
        await host.DecideAndDeliverAsync(ScheduleApprovalRouting.SubjectModule, ScheduleApprovalRouting.SubjectType, first, ApprovalTaskDecision.Approve);
        await AssertOneRowAsync(projectId, milestoneId, "baselined");

        // WF-03 reforecasts it, then rebaselines under a change authorisation: a second copy of the same row.
        DateOnly reforecast = planned.AddDays(5);
        object replanned = new
        {
            scheduleActivityId = activityId,
            title = new { text = "Works handed over", language = "en" },
            milestoneCategoryItemId = MilestoneTestHost.GeneralCategoryId,
            forecastDate = MilestoneDriver.Iso(reforecast),
        };
        using (HttpResponseMessage current = await client.GetAsync($"{MilestoneDriver.Milestones}/{milestoneId}", sessions.ProjectManager))
        using (HttpResponseMessage edited = await client.PutAsync($"{MilestoneDriver.Milestones}/{milestoneId}", sessions.ProjectManager, replanned, AdministrationApi.ETagOf(current)))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        }

        await AssertOneRowAsync(projectId, milestoneId, "reforecast");
        Guid second = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId, host.Rebaselines.Authorize(projectId)));
        await host.DecideAndDeliverAsync(ScheduleApprovalRouting.SubjectModule, ScheduleApprovalRouting.SubjectType, second, ApprovalTaskDecision.Approve);
        await AssertOneRowAsync(projectId, milestoneId, "rebaselined");
        Assert.Equal([$"{first} {MilestoneDriver.Iso(planned)}", $"{second} {MilestoneDriver.Iso(reforecast)}"], await host.Database.QueryAsync($"""
            SELECT b.id || ' ' || m.planned_date FROM schedule.baseline_milestone m JOIN schedule.project_baseline b ON b.id = m.project_baseline_id
            WHERE m.project_milestone_id = '{milestoneId}' ORDER BY b.version_no
            """));

        // WF-05 claims it and WF-11 accepts: WF-03 records the same row ACHIEVED; the accepted date stays WF-05's.
        DateOnly claimed = MilestoneDriver.Today.AddDays(-2);
        Guid accepted = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, claimed));
        await host.DecideAndDeliverAsync(accepted, ApprovalTaskDecision.Approve);
        await AssertOneRowAsync(projectId, milestoneId, "achieved");

        // WF-05 corrects the achievement: a new revision of the same milestone, never a new milestone.
        Guid correction = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, claimed.AddDays(-1)));
        await host.DecideAndDeliverAsync(correction, ApprovalTaskDecision.Approve);
        await AssertOneRowAsync(projectId, milestoneId, "corrected");

        // Both authorities' APIs show the one milestone: WF-03 as ACHIEVED against the ACTIVE baseline, WF-05's revisions naming it.
        JsonObject shared = Assert.Single(await client.ItemsAsync(sessions.ProjectManager, MilestoneDriver.Milestones, projectId))!.AsObject();
        Assert.Equal((milestoneId.ToString(), "ACHIEVED", second.ToString(), MilestoneDriver.Iso(reforecast)),
            (shared.Text("id"), shared.Text("status"), shared.Text("baselineId"), shared.Text("baselinePlannedDate")));
        JsonArray revisions = await client.AchievementPageAsync(sessions.ProjectManager, $"projectId={projectId}");
        Assert.Equal([(correction.ToString(), milestoneId.ToString(), "ACCEPTED"), (accepted.ToString(), milestoneId.ToString(), "SUPERSEDED")],
            revisions.Select(r => (r!.Text("id"), r!.Text("projectMilestoneId"), r!.Text("status"))));
    }

    /// <summary>
    /// The row the project has is this milestone and no other; every table that names a shared milestone is one of the two that may,
    /// and names only this one. A copy WF-05 kept, or a row a baseline or an acceptance created, fails here.
    /// </summary>
    private async Task AssertOneRowAsync(Guid projectId, Guid milestoneId, string after)
    {
        IReadOnlyList<string> rows = await host.Database.QueryAsync($"SELECT id::text FROM schedule.project_milestone WHERE project_id = '{projectId}'");
        Assert.True(rows.SequenceEqual([milestoneId.ToString()]), $"After the milestone was {after}, the project has {rows.Count} shared milestone rows: {string.Join(", ", rows)}");

        IReadOnlyList<string> references = await host.Database.QueryAsync("""
            SELECT DISTINCT n.nspname || '.' || t.relname || '.' || a.attname
            FROM pg_constraint c
            JOIN pg_class t ON t.oid = c.conrelid JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
            WHERE c.contype = 'f' AND c.confrelid = 'schedule.project_milestone'::regclass
            ORDER BY 1
            """);
        Assert.Equal(MilestoneReferences, references);
        Assert.All(await host.Database.QueryAsync($"""
            SELECT project_milestone_id::text FROM milestone.milestone_achievement WHERE project_id = '{projectId}'
            UNION SELECT m.project_milestone_id::text FROM schedule.baseline_milestone m JOIN schedule.project_baseline b ON b.id = m.project_baseline_id
            WHERE b.project_id = '{projectId}'
            """), id => Assert.Equal(milestoneId.ToString(), id));
    }
}
