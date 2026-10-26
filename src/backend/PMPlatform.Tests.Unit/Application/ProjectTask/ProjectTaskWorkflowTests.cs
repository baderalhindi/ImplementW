using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ProjectTask;
using PMPlatform.Domain.ProjectTask;

namespace PMPlatform.Tests.Unit.Application.ProjectTask;

/// <summary>
/// The task state machine of TASK-048: NOT_STARTED → IN_PROGRESS ⇄ BLOCKED → COMPLETED, with no way from BLOCKED to COMPLETED,
/// a reopen that needs its own permission, and CANCELLED final.
/// </summary>
public sealed class ProjectTaskWorkflowTests
{
    [Fact]
    public void TheWorkflowHasExactlyTheTenEdgesOfTask048()
    {
        Assert.Equal(
            [
                (ProjectTaskStatus.NotStarted, ProjectTaskStatus.InProgress),
                (ProjectTaskStatus.NotStarted, ProjectTaskStatus.Blocked),
                (ProjectTaskStatus.NotStarted, ProjectTaskStatus.Cancelled),
                (ProjectTaskStatus.InProgress, ProjectTaskStatus.Blocked),
                (ProjectTaskStatus.InProgress, ProjectTaskStatus.Completed),
                (ProjectTaskStatus.InProgress, ProjectTaskStatus.Cancelled),
                (ProjectTaskStatus.Blocked, ProjectTaskStatus.NotStarted),
                (ProjectTaskStatus.Blocked, ProjectTaskStatus.InProgress),
                (ProjectTaskStatus.Blocked, ProjectTaskStatus.Cancelled),
                (ProjectTaskStatus.Completed, ProjectTaskStatus.InProgress),
            ],
            ProjectTaskWorkflow.Transitions.OrderBy(t => t.From).ThenBy(t => t.To));
    }

    /// <summary>The acceptance criterion: a blocked task reaches COMPLETED only by being unblocked first.</summary>
    [Fact]
    public void ABlockedTaskCannotBeCompletedUntilItIsUnblocked()
    {
        Assert.False(ProjectTaskWorkflow.Allows(ProjectTaskStatus.Blocked, ProjectTaskStatus.Completed));
        Assert.Null(ProjectTaskWorkflow.TargetOf(TaskCommand.Complete, ProjectTaskStatus.Blocked, started: true));

        ProjectTaskStatus unblocked = ProjectTaskWorkflow.TargetOf(TaskCommand.Unblock, ProjectTaskStatus.Blocked, started: true)!.Value;
        Assert.Equal(ProjectTaskStatus.InProgress, unblocked);
        Assert.Equal(ProjectTaskStatus.Completed, ProjectTaskWorkflow.TargetOf(TaskCommand.Complete, unblocked, started: true));
    }

    [Fact]
    public void CompletedIsReachedOnlyFromInProgress() =>
        Assert.Equal([ProjectTaskStatus.InProgress], ProjectTaskWorkflow.Transitions.Where(t => t.To == ProjectTaskStatus.Completed).Select(t => t.From));

    [Fact]
    public void CompletedIsLeftOnlyByAReopenToInProgress()
    {
        Assert.Equal([ProjectTaskStatus.InProgress], ProjectTaskWorkflow.Transitions.Where(t => t.From == ProjectTaskStatus.Completed).Select(t => t.To));
        Assert.Equal(ProjectTaskStatus.InProgress, ProjectTaskWorkflow.TargetOf(TaskCommand.Reopen, ProjectTaskStatus.Completed, started: true));
        Assert.All(
            Enum.GetValues<TaskCommand>().Where(c => c != TaskCommand.Reopen),
            command => Assert.Null(ProjectTaskWorkflow.TargetOf(command, ProjectTaskStatus.Completed, started: true)));
    }

    [Fact]
    public void CancelledIsFinal()
    {
        Assert.DoesNotContain(ProjectTaskWorkflow.Transitions, t => t.From == ProjectTaskStatus.Cancelled);
        Assert.All(Enum.GetValues<TaskCommand>(), command => Assert.Null(ProjectTaskWorkflow.TargetOf(command, ProjectTaskStatus.Cancelled, started: true)));
    }

    /// <summary>Unblocking returns a task to where it was blocked from: one blocked before it started has not started.</summary>
    [Theory]
    [InlineData(true, ProjectTaskStatus.InProgress)]
    [InlineData(false, ProjectTaskStatus.NotStarted)]
    public void UnblockingReturnsTheTaskToWhereItWasBlockedFrom(bool started, ProjectTaskStatus expected) =>
        Assert.Equal(expected, ProjectTaskWorkflow.TargetOf(TaskCommand.Unblock, ProjectTaskStatus.Blocked, started));

    /// <summary>Every edge a command takes is one of the declared edges, and every declared edge is some command's.</summary>
    [Fact]
    public void TheCommandsTakeExactlyTheDeclaredEdges()
    {
        HashSet<(ProjectTaskStatus, ProjectTaskStatus)> taken = [];
        foreach (TaskCommand command in Enum.GetValues<TaskCommand>())
        {
            foreach (ProjectTaskStatus from in Enum.GetValues<ProjectTaskStatus>())
            {
                foreach (bool started in new[] { true, false })
                {
                    if (ProjectTaskWorkflow.TargetOf(command, from, started) is { } to)
                    {
                        taken.Add((from, to));
                    }
                }
            }
        }

        Assert.Equal(ProjectTaskWorkflow.Transitions.OrderBy(t => t.From).ThenBy(t => t.To), taken.OrderBy(t => t.Item1).ThenBy(t => t.Item2));
    }

    /// <summary>The acceptance criterion: reopening is decided on TASK_REOPEN, never on the general edit permissions; cancelling on TASK_MANAGE.</summary>
    [Fact]
    public void ReopenAndCancelAreDecidedOnTheirOwnPermissions()
    {
        Assert.Equal(PermissionCatalogue.TaskReopen, ProjectTaskWorkflow.PermissionOf(TaskCommand.Reopen));
        Assert.Equal(PermissionCatalogue.TaskManage, ProjectTaskWorkflow.PermissionOf(TaskCommand.Cancel));
        Assert.All(
            [TaskCommand.Start, TaskCommand.Block, TaskCommand.Unblock, TaskCommand.Complete],
            command => Assert.Equal(PermissionCatalogue.TaskUpdate, ProjectTaskWorkflow.PermissionOf(command)));
        Assert.Equal(1, Enum.GetValues<TaskCommand>().Count(c => ProjectTaskWorkflow.PermissionOf(c) == PermissionCatalogue.TaskReopen));
    }
}
