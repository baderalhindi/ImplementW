using PMPlatform.Tests.Integration.ProjectTask;

namespace PMPlatform.Tests.Integration.GlobalSearch;

[Collection(ProjectTaskSuite.Name)]
public sealed class GlobalSearchTaskTests(ProjectTaskTestHost host)
{
    /// <summary>An ASSIGNED grant reaches the person's own task and not their colleague's on the same project.</summary>
    [Fact]
    public async Task ATaskIsFoundExactlyByWhoeverMayOpenIt()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        string text = GlobalSearchDriver.UniqueText();
        Guid assigned = await client.TaskAsync(sessions.ProjectManager, projectId, $"Pour {text}", days: 3, assigneeUserId: ProjectTaskDriver.Person(6));
        Guid other = await client.TaskAsync(sessions.ProjectManager, projectId, $"Survey {text}", days: 3);

        await client.AssertFindableExactlyWhenReadableAsync(sessions.ProjectManager, text, "PROJECT_TASK", assigned, $"{ProjectTaskDriver.Tasks}/{assigned}", findable: true);
        await client.AssertFindableExactlyWhenReadableAsync(sessions.ProjectManager, text, "PROJECT_TASK", other, $"{ProjectTaskDriver.Tasks}/{other}", findable: true);
        await client.AssertFindableExactlyWhenReadableAsync(sessions.Owner, text, "PROJECT_TASK", assigned, $"{ProjectTaskDriver.Tasks}/{assigned}", findable: true);
        await client.AssertFindableExactlyWhenReadableAsync(sessions.Owner, text, "PROJECT_TASK", other, $"{ProjectTaskDriver.Tasks}/{other}", findable: false);
    }

    /// <summary>Each role holds the task in turn as its assignee, so an ASSIGNED grant is tried as well as every wider one.</summary>
    [Fact]
    public async Task EveryRoleFindsAnAssignedTaskExactlyWhenItsPageOpens()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        string text = GlobalSearchDriver.UniqueText();
        Guid taskId = await client.TaskAsync(sessions.ProjectManager, projectId, $"Pour {text}", days: 3);

        await client.AssertSearchAgreesWithThePageForEveryRoleAsync(
            text, "PROJECT_TASK", taskId, $"{ProjectTaskDriver.Tasks}/{taskId}",
            role => host.Database.ExecuteAsync($"UPDATE project_task.project_task SET assignee_user_id = '{ProjectTaskDriver.Person(role)}' WHERE id = '{taskId}'"));
    }
}
