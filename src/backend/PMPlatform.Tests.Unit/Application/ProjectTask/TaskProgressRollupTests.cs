using PMPlatform.Application.Features.ProjectTask;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Tests.Unit.Application.ProjectTask;

/// <summary>
/// ADR-009 at task level: a leaf's actual percentage as its owner entered it, a parent's the planned-duration-weighted mean of
/// its live subtasks, an activity's the same mean over its live leaf tasks; CANCELLED counts nowhere.
/// </summary>
public sealed class TaskProgressRollupTests
{
    private static readonly Guid ActivityId = Guid.NewGuid();

    /// <summary>
    /// TASK-048's acceptance criterion: a parent with five subtasks in mixed states. NOT_STARTED counts 0, IN_PROGRESS and
    /// BLOCKED their entered figure, COMPLETED 100, CANCELLED nothing — not even its weight:
    /// (0×2 + 50×4 + 25×2 + 100×2) / (2 + 4 + 2 + 2) = 450 / 10 = 45.
    /// </summary>
    [Fact]
    public void AParentIsTheDurationWeightedMeanOfItsSubtasksInMixedStates()
    {
        ProjectTaskEntity parent = Task(ProjectTaskStatus.InProgress, 20, percent: 90m);
        ProjectTaskEntity[] subtasks =
        [
            Subtask(parent, ProjectTaskStatus.NotStarted, 2),
            Subtask(parent, ProjectTaskStatus.InProgress, 4, 50m),
            Subtask(parent, ProjectTaskStatus.Blocked, 2, 25m),
            Subtask(parent, ProjectTaskStatus.Completed, 2, 100m),
            Subtask(parent, ProjectTaskStatus.Cancelled, 10, 80m),
        ];

        Assert.Equal(45m, TaskProgressRollup.PercentOf(parent, subtasks));
    }

    /// <summary>The same subtasks make the activity's figure: its leaves are the subtasks, not the parent, whose own entry no longer counts.</summary>
    [Fact]
    public void AnActivityIsRolledUpFromItsLiveLeavesOnly()
    {
        ProjectTaskEntity parent = Task(ProjectTaskStatus.InProgress, 20, percent: 90m);
        ProjectTaskEntity standalone = Task(ProjectTaskStatus.InProgress, 10, percent: 30m);
        ProjectTaskEntity elsewhere = Task(ProjectTaskStatus.Completed, 50, activityId: Guid.NewGuid());
        ProjectTaskEntity[] tasks =
        [
            parent, standalone, elsewhere,
            Subtask(parent, ProjectTaskStatus.NotStarted, 2),
            Subtask(parent, ProjectTaskStatus.InProgress, 4, 50m),
            Subtask(parent, ProjectTaskStatus.Blocked, 2, 25m),
            Subtask(parent, ProjectTaskStatus.Completed, 2, 100m),
            Subtask(parent, ProjectTaskStatus.Cancelled, 10, 80m),
        ];

        // (450 + 30×10) / (10 + 10) = 37.5
        Assert.Equal(37.5m, TaskProgressRollup.ActivityPercent(ActivityId, tasks));
    }

    [Fact]
    public void AParentWhoseSubtasksAreAllCancelledIsALeafAgain()
    {
        ProjectTaskEntity parent = Task(ProjectTaskStatus.InProgress, 20, percent: 60m);
        ProjectTaskEntity[] tasks = [parent, Subtask(parent, ProjectTaskStatus.Cancelled, 5, 10m)];

        Assert.Equal(60m, TaskProgressRollup.PercentOf(parent, tasks));
        Assert.Equal(60m, TaskProgressRollup.ActivityPercent(ActivityId, tasks));
    }

    [Fact]
    public void ACompletedTaskIsAHundredAndANotStartedOneWithoutAFigureIsZero()
    {
        Assert.Equal(100m, TaskProgressRollup.LeafPercent(Task(ProjectTaskStatus.Completed, 3)));
        Assert.Equal(0m, TaskProgressRollup.LeafPercent(Task(ProjectTaskStatus.NotStarted, 3)));
    }

    [Fact]
    public void AnActivityWithNoLiveTaskHasNoFigure()
    {
        ProjectTaskEntity[] tasks = [Task(ProjectTaskStatus.Cancelled, 5, percent: 40m)];

        Assert.Null(TaskProgressRollup.ActivityPercent(ActivityId, tasks));
        Assert.Null(TaskProgressRollup.ActivityPercent(Guid.NewGuid(), tasks));
    }

    /// <summary>Four places, as the columns hold them and WF-02 rounds them: 100 / 3 = 33.3333.</summary>
    [Fact]
    public void FiguresAreRoundedToFourPlaces()
    {
        ProjectTaskEntity parent = Task(ProjectTaskStatus.InProgress, 3);
        ProjectTaskEntity[] subtasks = [Subtask(parent, ProjectTaskStatus.Completed, 1), Subtask(parent, ProjectTaskStatus.NotStarted, 2)];

        Assert.Equal(33.3333m, TaskProgressRollup.PercentOf(parent, subtasks));
    }

    private static ProjectTaskEntity Task(ProjectTaskStatus status, int days, decimal? percent = null, Guid? activityId = null) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.Empty,
        ScheduleActivityId = activityId ?? ActivityId,
        Title = new NarrativeText("Task", Language.En),
        Status = status,
        PlannedDurationDays = days,
        ActualPercentComplete = percent,
    };

    private static ProjectTaskEntity Subtask(ProjectTaskEntity parent, ProjectTaskStatus status, int days, decimal? percent = null)
    {
        ProjectTaskEntity subtask = Task(status, days, percent, parent.ScheduleActivityId);
        subtask.ParentTaskId = parent.Id;
        return subtask;
    }
}
