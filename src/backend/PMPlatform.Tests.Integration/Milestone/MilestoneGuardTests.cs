using Npgsql;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Tests.Integration.Identity;
using PMPlatform.Tests.Integration.Schedule;

namespace PMPlatform.Tests.Integration.Milestone;

/// <summary>
/// Migrations <c>TASK-050_GuardMilestoneAchievement</c> and <c>TASK-050_GuardProjectMilestone</c>: the achievement history and
/// the shared milestone hold in the database whoever writes, not only through the services. Every statement runs in a
/// transaction that is rolled back.
/// </summary>
[Collection(MilestoneSuite.Name)]
public sealed class MilestoneGuardTests(MilestoneTestHost host)
{
    private static readonly Guid Writer = MilestoneDriver.Person(8);

    /// <summary>The acceptance criterion in the database: an accepted revision is never overwritten in place, whatever writes it.</summary>
    [Fact]
    public async Task AnAcceptedRevisionIsNeverOverwrittenInPlace()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, await host.ScheduledProjectAsync(client, sessions.ProjectManager));
        Guid accepted = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today.AddDays(-5)));
        await host.DecideAndDeliverAsync(accepted, ApprovalTaskDecision.Approve);
        Guid submitted = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));

        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is ACCEPTED: it is never edited",
            $"UPDATE milestone.milestone_achievement SET accepted_actual_achievement_date = accepted_actual_achievement_date - 1, claimed_achievement_date = claimed_achievement_date - 1 WHERE id = '{accepted}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is ACCEPTED: it is never edited",
            $"UPDATE milestone.milestone_achievement SET narrative = 'Rewritten', narrative_lang = 'en' WHERE id = '{accepted}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "ACCEPTED to DRAFT is not a step",
            $"UPDATE milestone.milestone_achievement SET status = 'DRAFT' WHERE id = '{accepted}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "only a DRAFT is deleted",
            $"DELETE FROM milestone.milestone_achievement WHERE id = '{accepted}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never truncated", "TRUNCATE milestone.milestone_achievement");

        // Superseded only by a later revision of its own milestone; a second ACCEPTED one the partial unique index refuses.
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_milestone_achievement_superseded_by",
            $"UPDATE milestone.milestone_achievement SET status = 'SUPERSEDED', superseded_by_achievement_id = '{accepted}' WHERE id = '{accepted}'");
        await AssertRefusedAsync(PostgresErrorCodes.UniqueViolation, "ix_milestone_achievement_accepted_project_milestone_id",
            $"UPDATE milestone.milestone_achievement SET status = 'ACCEPTED', accepted_actual_achievement_date = claimed_achievement_date, reviewed_at = now(), reviewed_by_user_id = '{MilestoneDriver.Person(2)}' WHERE id = '{submitted}'");

        // A submitted claim is what WF-11 reviews: it is not edited, and its accepted date is the date it claimed.
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "the claim WF-11 reviewed never changes",
            $"UPDATE milestone.milestone_achievement SET claimed_achievement_date = claimed_achievement_date - 1 WHERE id = '{submitted}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_milestone_achievement_accepted_date", $"""
            UPDATE milestone.milestone_achievement SET status = 'SUPERSEDED', superseded_by_achievement_id = '{submitted}' WHERE id = '{accepted}';
            UPDATE milestone.milestone_achievement SET status = 'ACCEPTED', accepted_actual_achievement_date = claimed_achievement_date - 1, reviewed_at = now(),
                   reviewed_by_user_id = '{MilestoneDriver.Person(2)}' WHERE id = '{submitted}'
            """);

        // Written whole and in order, a correction's acceptance is a step: the old one superseded first, then the new one accepted.
        await host.Database.ExecuteRolledBackAsync($"""
            UPDATE milestone.milestone_achievement SET status = 'SUPERSEDED', superseded_by_achievement_id = '{submitted}' WHERE id = '{accepted}';
            UPDATE milestone.milestone_achievement SET status = 'ACCEPTED', accepted_actual_achievement_date = claimed_achievement_date, reviewed_at = now(),
                   reviewed_by_user_id = '{MilestoneDriver.Person(2)}' WHERE id = '{submitted}'
            """);
    }

    [Fact]
    public async Task ARevisionIsBornDraftAfterEveryEarlierOneAndOneIsOpenAtATime()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid milestoneId = await client.MilestoneAsync(sessions.ProjectManager, projectId);
        Guid returned = AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));
        await host.DecideAndDeliverAsync(returned, ApprovalTaskDecision.Return, "Wrong date");
        Guid draft = AdministrationApi.IdOf(await client.ClaimAsync(sessions.ProjectManager, milestoneId, MilestoneDriver.Today));

        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is RETURNED and changes no more",
            $"UPDATE milestone.milestone_achievement SET return_reason = 'Rewritten', return_reason_lang = 'en' WHERE id = '{returned}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_milestone_achievement_born", Insert(milestoneId, projectId, 9, "SUBMITTED"));
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_milestone_achievement_revision_order", Insert(milestoneId, projectId, 1, "DRAFT"));
        await AssertRefusedAsync(PostgresErrorCodes.UniqueViolation, "ix_milestone_achievement_open_project_milestone_id", Insert(milestoneId, projectId, 3, "DRAFT"));
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "its milestone, project and revision never change",
            $"UPDATE milestone.milestone_achievement SET revision_no = 7 WHERE id = '{draft}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "DRAFT to ACCEPTED is not a step",
            $"UPDATE milestone.milestone_achievement SET status = 'ACCEPTED', accepted_actual_achievement_date = claimed_achievement_date, submitted_at = now(), submitted_by_user_id = '{Writer}', reviewed_at = now(), reviewed_by_user_id = '{Writer}' WHERE id = '{draft}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_milestone_achievement_submitted",
            $"UPDATE milestone.milestone_achievement SET status = 'SUBMITTED' WHERE id = '{draft}'");

        // A DRAFT is HARD_DRAFT, and its claim is edited.
        await host.Database.ExecuteRolledBackAsync($"UPDATE milestone.milestone_achievement SET claimed_achievement_date = claimed_achievement_date - 1 WHERE id = '{draft}'");
        await host.Database.ExecuteRolledBackAsync($"DELETE FROM milestone.milestone_achievement WHERE id = '{draft}'");
    }

    [Fact]
    public async Task TheSharedMilestoneIsRetainedAndAchievedOnlyFromPlanned()
    {
        using HttpClient client = host.Api.CreateClient();
        MilestoneSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid achieved = await client.MilestoneAsync(sessions.ProjectManager, projectId);
        await host.DecideAndDeliverAsync(AdministrationApi.IdOf(await client.SubmittedClaimAsync(sessions.ProjectManager, achieved, MilestoneDriver.Today)), ApprovalTaskDecision.Approve);
        Guid cancelled = await client.MilestoneAsync(sessions.ProjectManager, projectId);
        await client.CommandOrFailAsync(sessions.ProjectManager, $"{MilestoneDriver.Milestones}/{cancelled}/cancel");
        Guid otherProject = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        Guid otherActivity = await client.ActivityAsync(sessions.ProjectManager, otherProject, "1", 0, 3);

        // A baseline of the other project, with its copy of a milestone's date.
        await client.MilestoneAsync(sessions.ProjectManager, otherProject);
        Guid baselineId = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, otherProject));
        await host.DecideAndDeliverAsync(ScheduleApprovalRouting.SubjectModule, ScheduleApprovalRouting.SubjectType, baselineId, ApprovalTaskDecision.Approve);

        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is ACHIEVED and changes no more",
            $"UPDATE schedule.project_milestone SET forecast_date = forecast_date + 1 WHERE id = '{achieved}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is CANCELLED and changes no more",
            $"UPDATE schedule.project_milestone SET status = 'ACHIEVED' WHERE id = '{cancelled}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is retained and never deleted",
            $"DELETE FROM schedule.project_milestone WHERE id = '{cancelled}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "its project and schedule never change",
            $"UPDATE schedule.project_milestone SET project_id = '{otherProject}' WHERE id = '{achieved}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_project_milestone_born", MilestoneInsert(projectId, "ACHIEVED", null));
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_project_milestone_activity", MilestoneInsert(projectId, "PLANNED", otherActivity));
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_project_milestone_project", $"""
            INSERT INTO schedule.project_milestone (id, project_id, project_schedule_id, title, title_lang, milestone_category_item_id, forecast_date, status, sort_order, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), '{otherProject}', s.id, 'Wrong schedule', 'en', '{MilestoneTestHost.GeneralCategoryId}', current_date, 'PLANNED', 0, now(), '{Writer}', now(), '{Writer}'
            FROM schedule.project_schedule s WHERE s.project_id = '{projectId}'
            """);
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "a baseline's copy and never changes",
            $"UPDATE schedule.baseline_milestone SET planned_date = planned_date + 1 WHERE project_baseline_id = '{baselineId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "a baseline's copy and never changes",
            $"DELETE FROM schedule.baseline_milestone WHERE project_baseline_id = '{baselineId}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_baseline_milestone_open_baseline", $"""
            INSERT INTO schedule.baseline_milestone (id, project_baseline_id, project_milestone_id, planned_date, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{baselineId}', '{achieved}', current_date, now(), '{Writer}', now(), '{Writer}')
            """);
    }

    private static string Insert(Guid milestoneId, Guid projectId, int revision, string status) => $"""
        INSERT INTO milestone.milestone_achievement (id, project_milestone_id, project_id, revision_no, status, claimed_achievement_date, submitted_at, submitted_by_user_id,
                                                     created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{milestoneId}', '{projectId}', {revision}, '{status}', current_date,
                {(status == "DRAFT" ? "NULL" : "now()")}, {(status == "DRAFT" ? "NULL" : $"'{Writer}'")}, now(), '{Writer}', now(), '{Writer}')
        """;

    private static string MilestoneInsert(Guid projectId, string status, Guid? activityId) => $"""
        INSERT INTO schedule.project_milestone (id, project_id, project_schedule_id, schedule_activity_id, title, title_lang, milestone_category_item_id, forecast_date, status, sort_order,
                                                created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), '{projectId}', s.id, {(activityId is { } a ? $"'{a}'" : "NULL")}, 'Inserted', 'en', '{MilestoneTestHost.GeneralCategoryId}', current_date, '{status}', 0,
               now(), '{Writer}', now(), '{Writer}'
        FROM schedule.project_schedule s WHERE s.project_id = '{projectId}'
        """;

    private async Task AssertRefusedAsync(string sqlState, string message, string sql)
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(sql));
        Assert.True(
            refused.SqlState == sqlState && (refused.MessageText.Contains(message, StringComparison.Ordinal) || refused.ConstraintName == message),
            $"{refused.SqlState} {refused.ConstraintName} {refused.MessageText}");
    }
}
