using Npgsql;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Schedule;

/// <summary>
/// Migration <c>TASK-046_GuardScheduleHistory</c>: the baseline workflow and history hold in the database whoever writes, not
/// only through the services. Every statement runs in a transaction that is rolled back.
/// </summary>
[Collection(ScheduleSuite.Name)]
public sealed class ScheduleGuardTests(ScheduleTestHost host)
{
    [Fact]
    public async Task TheDatabaseHoldsTheBaselineWorkflowAndItsHistory()
    {
        using HttpClient client = host.Api.CreateClient();
        ScheduleSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ScheduledProjectAsync(client, sessions.ProjectManager);
        (Guid activity, Guid superseded) = await host.ApprovedBaselineAsync(client, sessions.ProjectManager, projectId);
        Guid active = AdministrationApi.IdOf(await client.SubmittedBaselineAsync(sessions.ProjectManager, projectId, host.Rebaselines.Authorize(projectId)));
        await host.DecideAndDeliverAsync(active, ApprovalTaskDecision.Approve);
        Guid draft = AdministrationApi.IdOf(await client.CreatedOrFailAsync(sessions.ProjectManager, ScheduleDriver.Baselines, new { projectId }));
        string otherProject = (await host.ProjectAsync()).ToString();

        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "it is only superseded",
            $"UPDATE schedule.project_baseline SET baseline_finish_date = baseline_finish_date + 30 WHERE id = '{active}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "changes no more",
            $"UPDATE schedule.project_baseline SET status = 'ACTIVE', superseded_at = NULL, superseded_by_baseline_id = NULL WHERE id = '{superseded}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is not a step of its workflow",
            $"UPDATE schedule.project_baseline SET status = 'REJECTED' WHERE id = '{draft}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never change",
            $"UPDATE schedule.project_baseline SET version_no = 9 WHERE id = '{draft}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "the revision moves by one",
            $"UPDATE schedule.project_baseline SET revision_no = 2 WHERE id = '{draft}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "only a DRAFT is deleted",
            $"DELETE FROM schedule.project_baseline WHERE id = '{superseded}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never truncated", "TRUNCATE schedule.project_baseline CASCADE");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "born DRAFT", $"""
            INSERT INTO schedule.project_baseline (id, project_id, baseline_type, version_no, revision_no, status, baseline_finish_date, activated_at, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{otherProject}', 'APPROVED', 1, 1, 'ACTIVE', current_date, now(), now(), '{ScheduleDriver.Person(8)}', now(), '{ScheduleDriver.Person(8)}')
            """);

        // An APPROVED baseline activates only with its copy, written before it activates and never changed.
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "activates with its copy",
            $"UPDATE schedule.project_baseline SET status = 'SUPERSEDED', superseded_at = now(), superseded_by_baseline_id = '{draft}' WHERE id = '{active}';"
            + $" UPDATE schedule.project_baseline SET status = 'ACTIVE', activated_at = now() WHERE id = '{draft}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "written before it activates", $"""
            INSERT INTO schedule.baseline_activity (id, project_baseline_id, schedule_activity_id, activity_kind, planned_start_date, planned_finish_date, planned_duration_days,
                                                    created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{active}', '{activity}', 'ACTIVITY', current_date, current_date, 1, now(), '{ScheduleDriver.Person(8)}', now(), '{ScheduleDriver.Person(8)}')
            """);
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never changes",
            $"UPDATE schedule.baseline_activity SET planned_finish_date = planned_finish_date + 1 WHERE project_baseline_id = '{active}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never changes",
            $"DELETE FROM schedule.baseline_activity WHERE project_baseline_id = '{superseded}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never truncated", "TRUNCATE schedule.baseline_activity");

        // The working schedule: retained, fixed to its project, a dependency never edited in place.
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never deleted", $"DELETE FROM schedule.project_schedule WHERE project_id = '{projectId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "stays so",
            $"UPDATE schedule.schedule_health_status SET project_id = '{otherProject}' WHERE project_id = '{projectId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "not a step WF-03 takes",
            $"UPDATE schedule.schedule_activity SET status = 'COMPLETED' WHERE id = '{activity}'");

        // The draft is still the planner's to delete under HARD_DRAFT.
        await host.Database.ExecuteRolledBackAsync($"DELETE FROM schedule.project_baseline WHERE id = '{draft}'");
    }

    private async Task AssertRefusedAsync(string sqlState, string message, string sql)
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(sql));
        Assert.True(refused.SqlState == sqlState && refused.MessageText.Contains(message, StringComparison.Ordinal), $"{refused.SqlState} {refused.MessageText}");
    }
}
