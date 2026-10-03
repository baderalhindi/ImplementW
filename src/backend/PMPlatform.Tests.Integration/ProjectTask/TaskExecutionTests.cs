using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>
/// A task's execution through the API: the state machine enforced by the server, the reopen that only TASK_REOPEN allows, the
/// actual percentage its owner maintains and the Project Manager may edit (ADR-009), and the cancel that ends a task for good.
/// </summary>
[Collection(ProjectTaskSuite.Name)]
public sealed class TaskExecutionTests(ProjectTaskTestHost host)
{
    /// <summary>
    /// TASK-048's acceptance criterion: a BLOCKED task is not completed — 409, and still BLOCKED in the database — until it is
    /// unblocked. Unblocked, it completes at 100%, and every step is audited.
    /// </summary>
    [Fact]
    public async Task ABlockedTaskCannotBeCompletedWithoutFirstBeingUnblocked()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid taskId = await client.TaskAsync(sessions.ProjectManager, projectId, "Excavation", 5, assigneeUserId: ProjectTaskDriver.Person(6));

        await client.CommandOrFailAsync(sessions.Owner, taskId, "start");
        JsonObject blocked = await client.BlockOrFailAsync(sessions.Owner, taskId);
        Assert.Equal("BLOCKED", blocked.Text("status"));
        Assert.Equal("Waiting for the site permit", blocked["blockedReason"]!.Text("text"));

        foreach (string token in new[] { sessions.Owner, sessions.ProjectManager })
        {
            using HttpResponseMessage refused = await client.CommandAsync(token, taskId, "complete");
            Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await refused.RefusalAsync());
        }

        Assert.Equal(["BLOCKED"], await host.StatusInDatabaseAsync(taskId));

        JsonObject unblocked = await client.CommandOrFailAsync(sessions.Owner, taskId, "unblock");
        Assert.Equal("IN_PROGRESS", unblocked.Text("status"));
        Assert.Null(unblocked["blockedReason"]);

        JsonObject completed = await client.CommandOrFailAsync(sessions.Owner, taskId, "complete");
        Assert.Equal(("COMPLETED", 100m, 100m), (completed.Text("status"), completed.Percent("actualPercentComplete"), completed.Percent("percentComplete")));
        Assert.NotNull(completed["completedAt"]);
        Assert.Equal(
            ["ProjectTask.TaskCreated", "ProjectTask.TaskStarted", "ProjectTask.TaskBlocked", "ProjectTask.TaskUnblocked", "ProjectTask.TaskCompleted"],
            await host.AuditEventsAsync(taskId));
    }

    /// <summary>A task blocked before it started goes back to NOT_STARTED when unblocked, and still has to be started.</summary>
    [Fact]
    public async Task ATaskBlockedBeforeItStartedIsNotStartedOnceUnblocked()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid taskId = await client.TaskAsync(sessions.ProjectManager, await host.ProjectAsync(), "Survey", 3);

        await client.BlockOrFailAsync(sessions.ProjectManager, taskId);
        JsonObject unblocked = await client.CommandOrFailAsync(sessions.ProjectManager, taskId, "unblock");

        Assert.Equal("NOT_STARTED", unblocked.Text("status"));
        Assert.Null(unblocked["actualStartDate"]);
        using HttpResponseMessage complete = await client.CommandAsync(sessions.ProjectManager, taskId, "complete");
        Assert.Equal((HttpStatusCode.Conflict, "INVALID_TRANSITION"), await complete.RefusalAsync());
    }

    /// <summary>
    /// TASK-048's acceptance criterion: reopening a completed task needs TASK_REOPEN, not general edit permission. local.r02 edits
    /// every task (TASK_UPDATE and TASK_MANAGE at ALL) and is refused, as is the task's owner; local.r05 holds TASK_REOPEN for the
    /// projects they manage, which this is not, so the task does not exist for them; the Project Manager, who holds it here, reopens.
    /// </summary>
    [Fact]
    public async Task ReopeningACompletedTaskRequiresTheReopenPermissionNotGeneralEditPermission()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        string otherManager = (await client.SignInOrFailAsync(5)).AccessToken;
        Guid taskId = await client.TaskAsync(sessions.ProjectManager, await host.ProjectAsync(), "Handover", 2, assigneeUserId: ProjectTaskDriver.Person(6));
        await client.CommandOrFailAsync(sessions.Owner, taskId, "start");
        await client.CommandOrFailAsync(sessions.Editor, taskId, "complete");

        foreach (string token in new[] { sessions.Editor, sessions.Owner })
        {
            using HttpResponseMessage refused = await client.CommandAsync(token, taskId, "reopen");
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using (HttpResponseMessage elsewhere = await client.CommandAsync(otherManager, taskId, "reopen"))
        {
            Assert.Equal(HttpStatusCode.NotFound, elsewhere.StatusCode);
        }

        Assert.Equal(["COMPLETED"], await host.StatusInDatabaseAsync(taskId));

        JsonObject reopened = await client.CommandOrFailAsync(sessions.ProjectManager, taskId, "reopen");
        Assert.Equal(("IN_PROGRESS", 1), (reopened.Text("status"), reopened["reopenedCount"]!.GetValue<int>()));
        Assert.Null(reopened["completedAt"]);
        Assert.Null(reopened["actualFinishDate"]);
        Assert.Contains("ProjectTask.TaskReopened", await host.AuditEventsAsync(taskId));
    }

    /// <summary>
    /// ADR-009: the owner maintains a leaf's actual percentage and the Project Manager may edit it. Someone else's task does not
    /// exist for the owner, and the figure is entered only while the task is under way.
    /// </summary>
    [Fact]
    public async Task TheOwnerMaintainsTheActualPercentageAndTheProjectManagerMayEditIt()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid owned = await client.TaskAsync(sessions.ProjectManager, projectId, "Owned", 4, assigneeUserId: ProjectTaskDriver.Person(6));
        Guid notOwned = await client.TaskAsync(sessions.ProjectManager, projectId, "Not owned", 4);

        using (HttpResponseMessage notYet = await client.CommandAsync(sessions.Owner, owned, "report-progress", new { actualPercentComplete = 10 }))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_PROGRESS_NOT_ENTERABLE"), await notYet.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.Owner, owned, "start");
        Assert.Equal(40m, (await client.ReportOrFailAsync(sessions.Owner, owned, 40m)).Percent("actualPercentComplete"));
        Assert.Equal(55.5m, (await client.ReportOrFailAsync(sessions.ProjectManager, owned, 55.5m)).Percent("actualPercentComplete"));

        using (HttpResponseMessage outOfRange = await client.CommandAsync(sessions.Owner, owned, "report-progress", new { actualPercentComplete = 101 }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
        }

        await client.CommandOrFailAsync(sessions.ProjectManager, notOwned, "start");
        using (HttpResponseMessage hidden = await client.CommandAsync(sessions.Owner, notOwned, "report-progress", new { actualPercentComplete = 10 }))
        {
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        Assert.Equal(
            ["ProjectTask.TaskCreated", "ProjectTask.TaskStarted", "ProjectTask.TaskProgressReported", "ProjectTask.TaskProgressReported"],
            await host.AuditEventsAsync(owned));
    }

    /// <summary>Tasks are planned once the project is APPROVED_PLANNED, and executed only once it is ACTIVE.</summary>
    [Fact]
    public async Task TasksArePlannedBeforeActivationButExecutedOnlyOnAnActiveProject()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid planned = await host.ProjectAsync("APPROVED_PLANNED");
        Guid taskId = await client.TaskAsync(sessions.ProjectManager, planned, "Mobilise", 3);

        using (HttpResponseMessage start = await client.CommandAsync(sessions.ProjectManager, taskId, "start"))
        {
            Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_PROJECT_NOT_ELIGIBLE"), await start.RefusalAsync());
        }

        using HttpResponseMessage draft = await client.PostAsync(ProjectTaskDriver.Tasks, sessions.ProjectManager, ProjectTaskDriver.TaskBody(await host.ProjectAsync("SUBMITTED"), "Too early", 1));
        Assert.Equal((HttpStatusCode.UnprocessableEntity, "TASK_PROJECT_NOT_ELIGIBLE"), await draft.RefusalAsync());
    }

    /// <summary>
    /// Cancelling is controlled: TASK_MANAGE, which the owner does not hold; not while a live subtask or a dependency needs the
    /// task; and final — a cancelled task changes no more.
    /// </summary>
    [Fact]
    public async Task CancellingIsControlledAndFinal()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid parent = await client.TaskAsync(sessions.ProjectManager, projectId, "Parent", 10, assigneeUserId: ProjectTaskDriver.Person(6));
        Guid subtask = await client.TaskAsync(sessions.ProjectManager, projectId, "Subtask", 5, parentTaskId: parent);

        using (HttpResponseMessage byOwner = await client.CommandAsync(sessions.Owner, parent, "cancel"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, byOwner.StatusCode);
        }

        using (HttpResponseMessage inUse = await client.CommandAsync(sessions.ProjectManager, parent, "cancel"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "TASK_IN_USE"), await inUse.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.ProjectManager, subtask, "cancel");
        JsonObject cancelled = await client.CommandOrFailAsync(sessions.ProjectManager, parent, "cancel");
        Assert.Equal("CANCELLED", cancelled.Text("status"));

        using (HttpResponseMessage start = await client.CommandAsync(sessions.ProjectManager, parent, "start"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "TASK_NOT_EDITABLE"), await start.RefusalAsync());
        }

        using HttpResponseMessage edit = await client.PutAsync($"{ProjectTaskDriver.Tasks}/{parent}", sessions.ProjectManager, ProjectTaskDriver.TaskBody(projectId, "Renamed", 2), "\"1\"");
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
    }

    /// <summary>A parent completes only when every live subtask has; a subtask reopens only under a parent that has not.</summary>
    [Fact]
    public async Task AParentCompletesAfterItsSubtasksAndASubtaskReopensOnlyUnderAnOpenParent()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid parent = await client.TaskAsync(sessions.ProjectManager, projectId, "Parent", 10);
        Guid first = await client.TaskAsync(sessions.ProjectManager, projectId, "First", 5, parentTaskId: parent);
        Guid second = await client.TaskAsync(sessions.ProjectManager, projectId, "Second", 5, parentTaskId: parent);
        foreach (Guid task in new[] { parent, first })
        {
            await client.CommandOrFailAsync(sessions.ProjectManager, task, "start");
        }

        await client.CommandOrFailAsync(sessions.ProjectManager, first, "complete");
        using (HttpResponseMessage early = await client.CommandAsync(sessions.ProjectManager, parent, "complete"))
        {
            Assert.Equal((HttpStatusCode.Conflict, "TASK_SUBTASKS_OPEN"), await early.RefusalAsync());
        }

        await client.CommandOrFailAsync(sessions.ProjectManager, second, "cancel");
        await client.CommandOrFailAsync(sessions.ProjectManager, parent, "complete");

        using HttpResponseMessage reopen = await client.CommandAsync(sessions.ProjectManager, first, "reopen");
        Assert.Equal((HttpStatusCode.Conflict, "TASK_SUBTASKS_OPEN"), await reopen.RefusalAsync());
    }
}
