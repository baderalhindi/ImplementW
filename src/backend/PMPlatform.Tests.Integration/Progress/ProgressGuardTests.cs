using Npgsql;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Progress;

/// <summary>
/// Migration <c>TASK-044_GuardProgressHistory</c>: the workflow and the history hold in the database whoever writes, not
/// only through <c>ProgressService</c>. Every statement runs in a transaction that is rolled back.
/// </summary>
[Collection(ProgressSuite.Name)]
public sealed class ProgressGuardTests(ProgressTestHost host)
{
    [Fact]
    public async Task TheDatabaseHoldsTheReviewWorkflowAndTheHistory()
    {
        using HttpClient client = host.Api.CreateClient();
        ProgressSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync(activatedDaysAgo: 10);
        host.Inputs.Set(projectId, actualPercent: 40, baselineStart: ProgressDriver.Today.AddDays(-49));
        Guid published = AdministrationApi.IdOf(await client.PublishedPeriodAsync(sessions.ProjectManager, sessions.Reviewer, projectId));
        Guid draft = AdministrationApi.IdOf(await client.StartOrFailAsync(sessions.ProjectManager, projectId));
        string closedCycle = Assert.Single(await host.Database.QueryAsync($"SELECT reporting_cycle_id::text FROM progress.progress_submission WHERE id = '{published}'"));
        string otherProject = (await host.ProjectAsync()).ToString();

        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is not a step of its review",
            $"UPDATE progress.progress_submission SET status = 'UNDER_REVIEW', submitted_at = now(), submitted_by_user_id = '{ProgressDriver.Person(8)}' WHERE id = '{draft}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "changes no more",
            $"UPDATE progress.progress_submission SET status = 'RETURNED' WHERE id = '{published}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never change",
            $"UPDATE progress.progress_submission SET revision_no = 7 WHERE id = '{draft}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "only a DRAFT is deleted",
            $"DELETE FROM progress.progress_submission WHERE id = '{published}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is CLOSED and changes no more",
            $"UPDATE progress.reporting_cycle SET status = 'OPEN' WHERE id = '{closedCycle}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never deleted",
            $"DELETE FROM progress.reporting_cycle WHERE project_id = '{projectId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never deleted",
            $"DELETE FROM progress.project_health_status WHERE project_id = '{projectId}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "stays so",
            $"UPDATE progress.project_health_status SET project_id = '{otherProject}' WHERE project_id = '{projectId}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "an OPEN period of its own project", Insert(closedCycle, "DRAFT", revision: 9));
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "born DRAFT", Insert(
            Assert.Single(await host.Database.QueryAsync($"SELECT reporting_cycle_id::text FROM progress.progress_submission WHERE id = '{draft}'")), "PUBLISHED", revision: 9));
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "of its own project and period", $"""
            INSERT INTO progress.published_progress_snapshot (id, project_id, reporting_cycle_id, progress_submission_id, published_at, published_by_user_id, actual_percent,
                                                              is_overridden, overall_health, health_rule_configuration_version_id, created_at, created_by, updated_at, updated_by)
            SELECT gen_random_uuid(), '{otherProject}', reporting_cycle_id, id, now(), '{ProgressDriver.Person(3)}', 1, false, 'GREEN', '{ProgressTestHost.WorkflowPolicyVersionId}',
                   now(), '{ProgressDriver.Person(3)}', now(), '{ProgressDriver.Person(3)}'
            FROM progress.progress_submission WHERE id = '{draft}'
            """);

        // The draft is still the person's to change and, under HARD_DRAFT, to delete.
        await host.Database.ExecuteRolledBackAsync($"UPDATE progress.progress_submission SET narrative = 'edited', narrative_lang = 'en' WHERE id = '{draft}'");
        await host.Database.ExecuteRolledBackAsync($"DELETE FROM progress.progress_submission WHERE id = '{draft}'");
    }

    private static string Insert(string cycleId, string status, int revision) => $"""
        INSERT INTO progress.progress_submission (id, project_id, reporting_cycle_id, revision_no, status, actual_percent_calculated, submitted_at, submitted_by_user_id,
                                                  reviewed_at, reviewed_by_user_id, created_at, created_by, updated_at, updated_by)
        SELECT gen_random_uuid(), project_id, id, {revision}, '{status}', 10,
               {(status == "DRAFT" ? "NULL, NULL" : $"now(), '{ProgressDriver.Person(8)}'")},
               {(status == "PUBLISHED" ? $"now(), '{ProgressDriver.Person(3)}'" : "NULL, NULL")},
               now(), '{ProgressDriver.Person(8)}', now(), '{ProgressDriver.Person(8)}'
        FROM progress.reporting_cycle WHERE id = '{cycleId}'
        """;

    private async Task AssertRefusedAsync(string sqlState, string message, string sql)
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(sql));
        Assert.True(refused.SqlState == sqlState && refused.MessageText.Contains(message, StringComparison.Ordinal), $"{refused.SqlState} {refused.MessageText}");
    }
}
