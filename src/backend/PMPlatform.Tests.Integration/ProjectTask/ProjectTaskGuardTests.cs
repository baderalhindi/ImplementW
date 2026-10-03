using Npgsql;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>
/// Migration <c>TASK-048_GuardProjectTask</c>: the task state machine, the blocking rules and the retained history hold in the
/// database whoever writes, not only through the services. Every statement runs in a transaction that is rolled back.
/// </summary>
[Collection(ProjectTaskSuite.Name)]
public sealed class ProjectTaskGuardTests(ProjectTaskTestHost host)
{
    [Fact]
    public async Task TheDatabaseHoldsTheTaskStateMachine()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid blocked = await client.TaskAsync(sessions.ProjectManager, projectId, "Blocked", 2);
        await client.CommandOrFailAsync(sessions.ProjectManager, blocked, "start");
        await client.BlockOrFailAsync(sessions.ProjectManager, blocked);
        Guid completed = await client.TaskAsync(sessions.ProjectManager, projectId, "Completed", 2);
        await client.CommandOrFailAsync(sessions.ProjectManager, completed, "start");
        await client.CommandOrFailAsync(sessions.ProjectManager, completed, "complete");
        Guid cancelled = await client.TaskAsync(sessions.ProjectManager, projectId, "Cancelled", 2);
        await client.CommandOrFailAsync(sessions.ProjectManager, cancelled, "cancel");

        // The acceptance criterion, in the database: BLOCKED → COMPLETED is no step, whatever else the statement sets right.
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "BLOCKED to COMPLETED is not a step",
            $"UPDATE project_task.project_task SET status = 'COMPLETED', blocked_reason = NULL, blocked_reason_lang = NULL, completed_at = now(), actual_finish_date = current_date WHERE id = '{blocked}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_project_task_blocked",
            $"UPDATE project_task.project_task SET status = 'IN_PROGRESS' WHERE id = '{blocked}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "the reopen count rises by one",
            $"UPDATE project_task.project_task SET status = 'IN_PROGRESS', completed_at = NULL, actual_finish_date = NULL WHERE id = '{completed}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is CANCELLED and changes no more",
            $"UPDATE project_task.project_task SET title = 'Revived' WHERE id = '{cancelled}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "its actual start never moves",
            $"UPDATE project_task.project_task SET actual_start_date = actual_start_date - 1 WHERE id = '{completed}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "its project and parent never change",
            $"UPDATE project_task.project_task SET project_id = '{await host.ProjectAsync()}' WHERE id = '{completed}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "is retained and never deleted",
            $"DELETE FROM project_task.project_task WHERE id = '{cancelled}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never truncated", "TRUNCATE project_task.project_task CASCADE");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "a task is born NOT_STARTED", $"""
            INSERT INTO project_task.project_task (id, project_id, title, title_lang, status, planned_start_date, planned_finish_date, planned_duration_days,
                                                   actual_start_date, created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{projectId}', 'Born started', 'en', 'IN_PROGRESS', current_date, current_date, 1, current_date,
                    now(), '{ProjectTaskDriver.Person(8)}', now(), '{ProjectTaskDriver.Person(8)}')
            """);
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "ck_project_task_planned",
            $"UPDATE project_task.project_task SET planned_duration_days = 9 WHERE id = '{blocked}'");

        // A reopen, written whole, is a step.
        await host.Database.ExecuteRolledBackAsync(
            $"UPDATE project_task.project_task SET status = 'IN_PROGRESS', completed_at = NULL, actual_finish_date = NULL, reopened_count = 1 WHERE id = '{completed}'");
    }

    [Fact]
    public async Task TheDatabaseHoldsTheBlockingRulesAndTheNetwork()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid a = await client.TaskAsync(sessions.ProjectManager, projectId, "A", 2);
        Guid b = await client.TaskAsync(sessions.ProjectManager, projectId, "B", 2);
        Guid parent = await client.TaskAsync(sessions.ProjectManager, projectId, "Parent", 2);
        await client.TaskAsync(sessions.ProjectManager, projectId, "Child", 1, parentTaskId: parent);
        Guid elsewhere = await client.TaskAsync(sessions.ProjectManager, await host.ProjectAsync(), "Elsewhere", 1);
        Guid link = await client.LinkOrFailAsync(sessions.ProjectManager, a, b, "FS");

        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "keeps it from starting",
            $"UPDATE project_task.project_task SET status = 'IN_PROGRESS', actual_start_date = current_date WHERE id = '{b}'");
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "it would close a cycle", Link(b, a));
        await AssertRefusedAsync(PostgresErrorCodes.CheckViolation, "both ends are tasks of one project", Link(a, elsewhere));
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "both ends are live leaf tasks", Link(a, parent));
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "never changed",
            $"UPDATE project_task.task_dependency SET dependency_type = 'SS' WHERE id = '{link}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "with a live subtask or a dependency is not cancelled",
            $"UPDATE project_task.project_task SET status = 'CANCELLED' WHERE id = '{a}'");
        await AssertRefusedAsync(PostgresErrorCodes.RestrictViolation, "a subtask is one level under", $"""
            INSERT INTO project_task.project_task (id, project_id, parent_task_id, title, title_lang, status, planned_start_date, planned_finish_date, planned_duration_days,
                                                   created_at, created_by, updated_at, updated_by)
            VALUES (gen_random_uuid(), '{projectId}', '{a}', 'Under a dependency end', 'en', 'NOT_STARTED', current_date, current_date, 1,
                    now(), '{ProjectTaskDriver.Person(8)}', now(), '{ProjectTaskDriver.Person(8)}')
            """);
    }

    private static string Link(Guid predecessor, Guid successor) => $"""
        INSERT INTO project_task.task_dependency (id, predecessor_task_id, successor_task_id, dependency_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{predecessor}', '{successor}', 'FS', now(), '{ProjectTaskDriver.Person(8)}', now(), '{ProjectTaskDriver.Person(8)}')
        """;

    private async Task AssertRefusedAsync(string sqlState, string message, string sql)
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => host.Database.ExecuteRolledBackAsync(sql));
        Assert.True(
            refused.SqlState == sqlState && (refused.MessageText.Contains(message, StringComparison.Ordinal) || refused.ConstraintName == message),
            $"{refused.SqlState} {refused.ConstraintName} {refused.MessageText}");
    }
}
