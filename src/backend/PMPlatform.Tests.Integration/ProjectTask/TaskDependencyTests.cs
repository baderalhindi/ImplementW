using System.Net;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>The blocking rules of task dependencies, enforced by the server: what a predecessor holds back, and which links are refused.</summary>
[Collection(ProjectTaskSuite.Name)]
public sealed class TaskDependencyTests(ProjectTaskTestHost host)
{
    /// <summary>FS: the successor cannot start until the predecessor is COMPLETED (422 TASK_DEPENDENCY_UNMET), then it can.</summary>
    [Fact]
    public async Task AFinishToStartPredecessorHoldsBackTheStart()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid design = await client.TaskAsync(sessions.ProjectManager, projectId, "Design", 5);
        Guid build = await client.TaskAsync(sessions.ProjectManager, projectId, "Build", 5);
        await client.LinkOrFailAsync(sessions.ProjectManager, design, build, "FS");

        await client.CommandOrFailAsync(sessions.ProjectManager, design, "start");
        using (HttpResponseMessage early = await client.CommandAsync(sessions.ProjectManager, build, "start"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_DEPENDENCY_UNMET"), await early.RefusalAsync());
        }

        Assert.Equal(["NOT_STARTED"], await host.StatusInDatabaseAsync(build));
        await client.CommandOrFailAsync(sessions.ProjectManager, design, "complete");
        Assert.Equal("IN_PROGRESS", (await client.CommandOrFailAsync(sessions.ProjectManager, build, "start")).Text("status"));
    }

    /// <summary>FF: the successor may start, but cannot complete until the predecessor has.</summary>
    [Fact]
    public async Task AFinishToFinishPredecessorHoldsBackTheCompletionOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid testing = await client.TaskAsync(sessions.ProjectManager, projectId, "Testing", 5);
        Guid documentation = await client.TaskAsync(sessions.ProjectManager, projectId, "Documentation", 5);
        await client.LinkOrFailAsync(sessions.ProjectManager, testing, documentation, "FF");

        await client.CommandOrFailAsync(sessions.ProjectManager, documentation, "start");
        using (HttpResponseMessage early = await client.CommandAsync(sessions.ProjectManager, documentation, "complete"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_DEPENDENCY_UNMET"), await early.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.ProjectManager, testing, "start");
        await client.CommandOrFailAsync(sessions.ProjectManager, testing, "complete");
        Assert.Equal("COMPLETED", (await client.CommandOrFailAsync(sessions.ProjectManager, documentation, "complete")).Text("status"));
    }

    /// <summary>A → B → C, and C → A is refused when it is saved; so is a link to itself, a link already there, and one already broken.</summary>
    [Fact]
    public async Task CircularDuplicateAndBrokenLinksAreRefused()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid a = await client.TaskAsync(sessions.ProjectManager, projectId, "A", 2);
        Guid b = await client.TaskAsync(sessions.ProjectManager, projectId, "B", 2);
        Guid c = await client.TaskAsync(sessions.ProjectManager, projectId, "C", 2);
        await client.LinkOrFailAsync(sessions.ProjectManager, a, b);
        await client.LinkOrFailAsync(sessions.ProjectManager, b, c);

        using (HttpResponseMessage cycle = await client.LinkAsync(sessions.ProjectManager, c, a))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_DEPENDENCY_CIRCULAR"), await cycle.RefusalAsync());
        }

        using (HttpResponseMessage self = await client.LinkAsync(sessions.ProjectManager, a, a))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_DEPENDENCY_INVALID"), await self.RefusalAsync());
        }

        using (HttpResponseMessage again = await client.LinkAsync(sessions.ProjectManager, a, b))
        {
            Assert.Equal((HttpStatusCode.Conflict, "TASK_DEPENDENCY_EXISTS"), await again.RefusalAsync());
        }

        // D is under way while E has not started: "D waits for E to finish" would already be broken.
        Guid d = await client.TaskAsync(sessions.ProjectManager, projectId, "D", 2);
        Guid e = await client.TaskAsync(sessions.ProjectManager, projectId, "E", 2);
        await client.CommandOrFailAsync(sessions.ProjectManager, d, "start");
        using HttpResponseMessage broken = await client.LinkAsync(sessions.ProjectManager, e, d, "FS");
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_DEPENDENCY_UNMET"), await broken.RefusalAsync());
        Assert.Equal(2, (await client.ItemsAsync(sessions.ProjectManager, ProjectTaskDriver.Dependencies, projectId)).Count);
    }

    /// <summary>
    /// A dependency joins leaves: a parent is no end, and an end takes no subtask. A task with a dependency is not cancelled until it
    /// is removed (R-40: removing one twice is 204 both times).
    /// </summary>
    [Fact]
    public async Task ADependencyJoinsLeafTasksOnly()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid parent = await client.TaskAsync(sessions.ProjectManager, projectId, "Parent", 4);
        await client.TaskAsync(sessions.ProjectManager, projectId, "Child", 2, parentTaskId: parent);
        Guid leaf = await client.TaskAsync(sessions.ProjectManager, projectId, "Leaf", 2);
        Guid other = await client.TaskAsync(sessions.ProjectManager, projectId, "Other", 2);

        using (HttpResponseMessage toParent = await client.LinkAsync(sessions.ProjectManager, leaf, parent))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_DEPENDENCY_INVALID"), await toParent.RefusalAsync());
            Assert.Equal(["successorTaskId NOT_ALLOWED"], await toParent.ReadFieldErrorsAsync());
        }

        Guid link = await client.LinkOrFailAsync(sessions.ProjectManager, leaf, other);
        using (HttpResponseMessage underEnd = await client.PostAsync(ProjectTaskDriver.Tasks, sessions.ProjectManager, ProjectTaskDriver.TaskBody(projectId, "Sub", 1, parentTaskId: leaf)))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_HIERARCHY_INVALID"), await underEnd.RefusalAsync());
        }

        using (HttpResponseMessage inUse = await client.CommandAsync(sessions.ProjectManager, other, "cancel"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "TASK_IN_USE"), await inUse.RefusalAsync());
        }

        for (int i = 0; i < 2; i++)
        {
            using HttpResponseMessage removed = await client.SendAsync(HttpMethod.Delete, $"{ProjectTaskDriver.Dependencies}/{link}", sessions.ProjectManager);
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        }

        await client.CommandOrFailAsync(sessions.ProjectManager, other, "cancel");
    }
}
