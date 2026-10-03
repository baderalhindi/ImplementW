using System.Globalization;
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

    /// <summary>Mixed-state subtask sets, as "STATUS days [percent]"; each expected figure is worked by hand beside it.</summary>
    public static TheoryData<string[], decimal> MixedStateScenarios() => new()
    {
        // (0×2 + 50×4 + 25×2 + 100×2) / 10 — the cancelled 10 days carry no weight.
        { ["NotStarted 2", "InProgress 4 50", "Blocked 2 25", "Completed 2 100", "Cancelled 10 80"], 45m },
        // (30×5 + 100×5) / 10
        { ["InProgress 5 30", "Completed 5 100", "Cancelled 20 90"], 65m },
        // (40×3 + 0×1 + 10×6 + 100×2) / 12 = 380 / 12
        { ["Blocked 3 40", "NotStarted 1", "Blocked 6 10", "Completed 2 100"], 31.6667m },
        // Every live subtask done: 100, whatever the cancelled one held.
        { ["Completed 4 100", "Completed 6 100", "Cancelled 3 20"], 100m },
        // Started but nothing entered yet, beside one not started: 0.
        { ["InProgress 2", "NotStarted 2", "Cancelled 2 70"], 0m },
    };

    /// <summary>
    /// TASK-048's acceptance criterion across five mixed-state subtask sets: the parent and the activity it executes against both
    /// stand at the planned-duration-weighted mean of the live subtasks.
    /// </summary>
    [Theory]
    [MemberData(nameof(MixedStateScenarios))]
    public void MixedStateSubtasksRollUpIntoTheParentAndTheActivity(string[] subtaskSpecs, decimal expected)
    {
        ArgumentNullException.ThrowIfNull(subtaskSpecs);
        ProjectTaskEntity parent = Task(ProjectTaskStatus.InProgress, 30, percent: 99m);
        List<ProjectTaskEntity> tasks = [parent];
        foreach (string[] spec in subtaskSpecs.Select(s => s.Split(' ')))
        {
            tasks.Add(Subtask(
                parent, Enum.Parse<ProjectTaskStatus>(spec[0]), int.Parse(spec[1], CultureInfo.InvariantCulture),
                spec.Length > 2 ? decimal.Parse(spec[2], CultureInfo.InvariantCulture) : null));
        }

        Assert.Equal(expected, TaskProgressRollup.PercentOf(parent, tasks));
        Assert.Equal(expected, TaskProgressRollup.ActivityPercent(ActivityId, tasks));
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
