using System.Net;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>The WF-04 surface's gates: 401, 403 at the gate, R-47's 404 and R-3's empty collection, R-21's If-Match, and who sees what.</summary>
[Collection(ProjectTaskSuite.Name)]
public sealed class ProjectTaskEndpointTests(ProjectTaskTestHost host)
{
    [Fact]
    public async Task EveryOperationNeedsASessionAndThePermission()
    {
        using HttpClient client = host.Api.CreateClient();
        Guid projectId = await host.ProjectAsync();
        string noTaskGrant = (await client.SignInOrFailAsync(1)).AccessToken;

        using (HttpResponseMessage anonymous = await client.GetAsync($"{ProjectTaskDriver.Tasks}?projectId={projectId}"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        foreach (string collection in new[] { ProjectTaskDriver.Tasks, ProjectTaskDriver.Dependencies, ProjectTaskDriver.ActivityProgress })
        {
            using HttpResponseMessage refused = await client.GetAsync($"{collection}?projectId={projectId}", noTaskGrant);
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{collection}: {(int)refused.StatusCode}");
        }

        using HttpResponseMessage create = await client.PostAsync(ProjectTaskDriver.Tasks, noTaskGrant, ProjectTaskDriver.TaskBody(projectId, "No", 1));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    /// <summary>
    /// ADR-013 and R-47: the entity Project Manager reaches the projects they manage only — another manager's project is an empty
    /// collection and a 404 — while a task owner sees exactly the tasks they own, and may not create one.
    /// </summary>
    [Fact]
    public async Task AManagerReachesTheirProjectsAndAnOwnerTheirTasks()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        string otherManager = (await client.SignInOrFailAsync(5)).AccessToken;
        Guid theirs = await host.ProjectAsync();
        Guid owned = await client.TaskAsync(sessions.ProjectManager, theirs, "Owned", 2, assigneeUserId: ProjectTaskDriver.Person(6));
        await client.TaskAsync(sessions.ProjectManager, theirs, "Not owned", 2);
        Guid someoneElses = await host.ProjectAsync(projectManager: 5);
        Guid hiddenTask = await client.TaskAsync(otherManager, someoneElses, "Elsewhere", 2);

        Assert.Equal(2, (await client.ItemsAsync(sessions.ProjectManager, ProjectTaskDriver.Tasks, theirs)).Count);
        Assert.Empty(await client.ItemsAsync(sessions.ProjectManager, ProjectTaskDriver.Tasks, someoneElses));
        using (HttpResponseMessage hidden = await client.GetAsync($"{ProjectTaskDriver.Tasks}/{hiddenTask}", sessions.ProjectManager))
        {
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        using (HttpResponseMessage notTheirs = await client.PostAsync(ProjectTaskDriver.Tasks, sessions.ProjectManager, ProjectTaskDriver.TaskBody(someoneElses, "No", 1)))
        {
            Assert.Equal(HttpStatusCode.NotFound, notTheirs.StatusCode);
        }

        Assert.Equal([owned.ToString()], (await client.ItemsAsync(sessions.Owner, ProjectTaskDriver.Tasks, theirs)).Select(t => t!.Text("id")));
        Assert.Empty(await client.ItemsAsync(sessions.Owner, ProjectTaskDriver.Dependencies, theirs));
        using HttpResponseMessage ownerCreates = await client.PostAsync(ProjectTaskDriver.Tasks, sessions.Owner, ProjectTaskDriver.TaskBody(theirs, "No", 1));
        Assert.Equal(HttpStatusCode.Forbidden, ownerCreates.StatusCode);
    }

    /// <summary>R-21: an edit needs If-Match, and a stale one is 412; a fresh one replaces the plan and derives the duration again.</summary>
    [Fact]
    public async Task AnEditNeedsTheCurrentVersion()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        using HttpResponseMessage created = await client.PostAsync(ProjectTaskDriver.Tasks, sessions.ProjectManager, ProjectTaskDriver.TaskBody(projectId, "Plan", 3));
        string etag = AdministrationApi.ETagOf(created);
        Guid taskId = AdministrationApi.IdOf(await created.ReadObjectAsync());
        object changed = ProjectTaskDriver.TaskBody(projectId, "Replanned", 8);

        using (HttpResponseMessage missing = await client.PutAsync($"{ProjectTaskDriver.Tasks}/{taskId}", sessions.ProjectManager, changed, null))
        {
            Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        }

        using (HttpResponseMessage fresh = await client.PutAsync($"{ProjectTaskDriver.Tasks}/{taskId}", sessions.ProjectManager, changed, etag))
        {
            Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
            Assert.Equal(("Replanned", 8), ((await fresh.ReadObjectAsync())["title"]!.Text("text"), (await client.GetTaskAsync(sessions.ProjectManager, taskId))["plannedDurationDays"]!.GetValue<int>()));
        }

        using HttpResponseMessage stale = await client.PutAsync($"{ProjectTaskDriver.Tasks}/{taskId}", sessions.ProjectManager, changed, etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
    }
}
