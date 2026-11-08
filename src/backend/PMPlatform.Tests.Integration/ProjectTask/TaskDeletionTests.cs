using System.Net;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>
/// Client review, item 8: a task is deleted with its subtasks and its dependencies; all of it leaves the reads and stays in the database,
/// and a task on the other end of a dependency is untouched.
/// </summary>
[Collection(ProjectTaskSuite.Name)]
public sealed class TaskDeletionTests(ProjectTaskTestHost host)
{
    [Fact]
    public async Task ATaskGoesWithItsSubtasksAndDependenciesAndItsRowsStay()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid parent = await client.TaskAsync(sessions.ProjectManager, projectId, "Site preparation", 5);
        Guid subtask = await client.TaskAsync(sessions.ProjectManager, projectId, "Clear the site", 2, parentTaskId: parent);
        Guid design = await client.TaskAsync(sessions.ProjectManager, projectId, "Design", 5);
        Guid build = await client.TaskAsync(sessions.ProjectManager, projectId, "Build", 5);
        await client.LinkOrFailAsync(sessions.ProjectManager, design, build, "FS");

        foreach (Guid deleted in new[] { parent, design })
        {
            using HttpResponseMessage response = await client.SendAsync(HttpMethod.Delete, $"{ProjectTaskDriver.Tasks}/{deleted}", sessions.ProjectManager);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        foreach (Guid hidden in new[] { parent, subtask, design })
        {
            using HttpResponseMessage read = await client.GetAsync($"{ProjectTaskDriver.Tasks}/{hidden}", sessions.ProjectManager);
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        }

        Assert.Equal([build.ToString()], (await client.ItemsAsync(sessions.ProjectManager, ProjectTaskDriver.Tasks, projectId)).Select(t => t!["id"]!.GetValue<string>()));
        Assert.Empty(await client.ItemsAsync(sessions.ProjectManager, ProjectTaskDriver.Dependencies, projectId));
        Assert.Equal(["3"], await host.Database.QueryAsync(
            $"SELECT count(*)::text FROM project_task.project_task WHERE project_id = '{projectId}' AND deleted_at IS NOT NULL"));
        Assert.Contains("ProjectTask.ProjectTaskDeleted", await host.AuditEventsAsync(parent));
        Assert.Contains("ProjectTask.ProjectTaskDeleted", await host.AuditEventsAsync(subtask));
    }
}
