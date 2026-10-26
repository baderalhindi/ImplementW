using Npgsql;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>
/// The project's task lock: two writers that would together close a cycle — A → B and B → A, each acyclic alone — cannot both
/// commit, because the second waits for the first and then sees its edge.
/// </summary>
[Collection(ProjectTaskSuite.Name)]
public sealed class TaskConcurrencyTests(ProjectTaskTestHost host)
{
    [Fact]
    public async Task TwoLinksThatTogetherCloseACycleCannotBothCommit()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid a = await client.TaskAsync(sessions.ProjectManager, projectId, "A", 2);
        Guid b = await client.TaskAsync(sessions.ProjectManager, projectId, "B", 2);

        await using NpgsqlConnection first = new(host.Database.ConnectionString);
        await using NpgsqlConnection second = new(host.Database.ConnectionString);
        await first.OpenAsync();
        await second.OpenAsync();
        await using NpgsqlTransaction firstWork = await first.BeginTransactionAsync();
        await using NpgsqlTransaction secondWork = await second.BeginTransactionAsync();

        await ExecuteAsync(first, firstWork, Link(a, b));

        // The second insert blocks on the project's lock until the first commits, then finds A → B and refuses B → A.
        Task secondInsert = ExecuteAsync(second, secondWork, Link(b, a));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.False(secondInsert.IsCompleted);
        await firstWork.CommitAsync();

        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(() => secondInsert);
        Assert.Equal(("23514", "ck_task_dependency_acyclic"), (refused.SqlState, refused.ConstraintName));
        Assert.Equal(["1"], await host.Database.QueryAsync(
            $"SELECT count(*)::text FROM project_task.task_dependency WHERE predecessor_task_id IN ('{a}', '{b}')"));
    }

    private static string Link(Guid predecessor, Guid successor) => $"""
        INSERT INTO project_task.task_dependency (id, predecessor_task_id, successor_task_id, dependency_type, created_at, created_by, updated_at, updated_by)
        VALUES (gen_random_uuid(), '{predecessor}', '{successor}', 'FS', now(), '{ProjectTaskDriver.Person(8)}', now(), '{ProjectTaskDriver.Person(8)}')
        """;

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        await using NpgsqlCommand command = new(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }
}
