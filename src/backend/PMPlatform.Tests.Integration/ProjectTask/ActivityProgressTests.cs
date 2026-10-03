using System.Net;
using System.Text.Json.Nodes;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.ProjectTask;

/// <summary>
/// The Activity Execution Progress WF-04 owns (ADR-009): rolled up from the live leaf tasks of each schedule activity, weighted by
/// planned duration, in the same transaction as the change that moved it; a parent's percentage computed on read.
/// </summary>
[Collection(ProjectTaskSuite.Name)]
public sealed class ActivityProgressTests(ProjectTaskTestHost host)
{
    /// <summary>
    /// Subtasks in mixed states under one parent: NOT_STARTED (2 days), IN_PROGRESS at 50% (4), BLOCKED at 25% (2), COMPLETED (2)
    /// and CANCELLED (10). The parent and its activity both stand at (0×2 + 50×4 + 25×2 + 100×2) / 10 = 45%; a second activity is
    /// rolled up apart, and every move is audited on the row.
    /// </summary>
    [Fact]
    public async Task SubtasksInMixedStatesRollUpIntoTheParentAndTheActivity()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid[] activities = await host.ActivitiesAsync(projectId, "1", "2");
        string pm = sessions.ProjectManager;

        Guid parent = await client.TaskAsync(pm, projectId, "Structure", 20, activities[0]);
        Guid notStarted = await client.TaskAsync(pm, projectId, "Formwork", 2, parentTaskId: parent);
        Guid inProgress = await client.TaskAsync(pm, projectId, "Rebar", 4, parentTaskId: parent);
        Guid blocked = await client.TaskAsync(pm, projectId, "Pour", 2, parentTaskId: parent);
        Guid completed = await client.TaskAsync(pm, projectId, "Survey", 2, parentTaskId: parent);
        Guid cancelled = await client.TaskAsync(pm, projectId, "Piling", 10, parentTaskId: parent);
        Guid elsewhere = await client.TaskAsync(pm, projectId, "Fit-out", 3, activities[1]);

        foreach (Guid task in new[] { inProgress, blocked, completed })
        {
            await client.CommandOrFailAsync(pm, task, "start");
        }

        await client.ReportOrFailAsync(pm, inProgress, 50m);
        await client.ReportOrFailAsync(pm, blocked, 25m);
        await client.BlockOrFailAsync(pm, blocked);
        await client.CommandOrFailAsync(pm, completed, "complete");
        await client.CommandOrFailAsync(pm, cancelled, "cancel");
        await client.CommandOrFailAsync(pm, elsewhere, "start");
        await client.ReportOrFailAsync(pm, elsewhere, 10m);

        JsonObject parentTask = await client.GetTaskAsync(pm, parent);
        Assert.Equal((activities[0].ToString(), 45m, 4), (parentTask.Text("scheduleActivityId"), parentTask.Percent("percentComplete"), parentTask["subtaskCount"]!.GetValue<int>()));
        Assert.Null(parentTask["actualPercentComplete"]);
        Assert.Equal(activities[0].ToString(), (await client.GetTaskAsync(pm, notStarted)).Text("scheduleActivityId"));

        Dictionary<string, decimal> progress = (await client.ItemsAsync(pm, ProjectTaskDriver.ActivityProgress, projectId))
            .ToDictionary(p => p!.Text("scheduleActivityId"), p => p!.Percent("actualPercentComplete"));
        Assert.Equal(new Dictionary<string, decimal> { [activities[0].ToString()] = 45m, [activities[1].ToString()] = 10m }, progress);

        Assert.Equal(["45.0000"], await host.Database.QueryAsync(
            $"SELECT actual_percent_complete::text FROM project_task.activity_execution_progress WHERE schedule_activity_id = '{activities[0]}'"));
        Assert.Contains("ProjectTask.ActivityProgressRecomputed", await host.Database.QueryAsync(
            $"SELECT DISTINCT event_type FROM audit_activity.audit_event WHERE subject_module = 'ProjectTask' AND subject_type = 'ActivityExecutionProgress'"));
    }

    /// <summary>
    /// A task executes against a live leaf activity of its own project, and a subtask against its parent's: an activity of another
    /// project, an unknown one, or another than the parent's is refused, and so are an owner with no role over the project and a
    /// priority that is not PUBLISHED.
    /// </summary>
    [Fact]
    public async Task ATaskNamesOnlyWhatItsProjectAllows()
    {
        using HttpClient client = host.Api.CreateClient();
        TaskSessions sessions = await client.SignInAsync();
        Guid projectId = await host.ProjectAsync();
        Guid[] mine = await host.ActivitiesAsync(projectId, "1", "2");
        Guid[] theirs = await host.ActivitiesAsync(await host.ProjectAsync(), "1");
        Guid parent = await client.TaskAsync(sessions.ProjectManager, projectId, "Parent", 5, mine[0]);

        (object Body, string Code, string Field)[] refused =
        [
            (ProjectTaskDriver.TaskBody(projectId, "Other project", 1, theirs[0]), "TASK_SCHEDULE_ACTIVITY_INVALID", "scheduleActivityId NOT_FOUND"),
            (ProjectTaskDriver.TaskBody(projectId, "Unknown", 1, Guid.NewGuid()), "TASK_SCHEDULE_ACTIVITY_INVALID", "scheduleActivityId NOT_FOUND"),
            (ProjectTaskDriver.TaskBody(projectId, "Not the parent's", 1, mine[1], parent), "TASK_SCHEDULE_ACTIVITY_INVALID", "scheduleActivityId NOT_ALLOWED"),
            (ProjectTaskDriver.TaskBody(projectId, "Disabled owner", 1, assigneeUserId: ProjectTaskDriver.Person(4)), "TASK_ASSIGNEE_NOT_ELIGIBLE", "assigneeUserId NOT_ALLOWED"),
            (new { projectId, title = new { text = "Draft priority", language = "en" }, priorityItemId = ProjectTaskTestHost.DraftPriorityId,
                   plannedStartDate = ProjectTaskDriver.Iso(ProjectTaskDriver.Day1), plannedFinishDate = ProjectTaskDriver.Iso(ProjectTaskDriver.Day1) },
             "TASK_PRIORITY_INVALID", "priorityItemId NOT_FOUND"),
        ];
        foreach ((object body, string code, string field) in refused)
        {
            using HttpResponseMessage response = await client.PostAsync(ProjectTaskDriver.Tasks, sessions.ProjectManager, body);
            Assert.Equal((HttpStatusCode.UnprocessableEntity, code), await response.RefusalAsync());
            Assert.Equal([field], await response.ReadFieldErrorsAsync());
        }

        JsonObject accepted = await client.CreatedOrFailAsync(sessions.ProjectManager, ProjectTaskDriver.Tasks, new
        {
            projectId,
            parentTaskId = parent,
            title = new { text = "Owned and prioritised", language = "en" },
            assigneeUserId = ProjectTaskDriver.Person(6),
            priorityItemId = ProjectTaskTestHost.PriorityId,
            plannedStartDate = ProjectTaskDriver.Iso(ProjectTaskDriver.Day1),
            plannedFinishDate = ProjectTaskDriver.Iso(ProjectTaskDriver.Day1.AddDays(6)),
            plannedDurationDays = 99,
            status = "COMPLETED",
        });
        Assert.Equal((mine[0].ToString(), 7, "NOT_STARTED"), (accepted.Text("scheduleActivityId"), accepted["plannedDurationDays"]!.GetValue<int>(), accepted.Text("status")));
    }
}
