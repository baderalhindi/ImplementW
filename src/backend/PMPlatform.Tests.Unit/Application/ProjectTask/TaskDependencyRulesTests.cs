using PMPlatform.Application.Features.ProjectTask;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Tests.Unit.Application.ProjectTask;

/// <summary>The blocking rules: FS and SS gate the successor's start, FF and SF its completion; F waits for COMPLETED, S for a start.</summary>
public sealed class TaskDependencyRulesTests
{
    [Theory]
    [InlineData(TaskDependencyType.Fs, true)]
    [InlineData(TaskDependencyType.Ss, true)]
    [InlineData(TaskDependencyType.Ff, false)]
    [InlineData(TaskDependencyType.Sf, false)]
    public void FsAndSsGateTheStartFfAndSfTheCompletion(TaskDependencyType type, bool gatesStart) =>
        Assert.Equal(gatesStart, TaskDependencyRules.GatesStart(type));

    [Theory]
    [InlineData(TaskDependencyType.Fs, ProjectTaskStatus.InProgress, false)]
    [InlineData(TaskDependencyType.Fs, ProjectTaskStatus.Completed, true)]
    [InlineData(TaskDependencyType.Ss, ProjectTaskStatus.NotStarted, false)]
    [InlineData(TaskDependencyType.Ss, ProjectTaskStatus.InProgress, true)]
    [InlineData(TaskDependencyType.Ff, ProjectTaskStatus.Blocked, false)]
    [InlineData(TaskDependencyType.Ff, ProjectTaskStatus.Completed, true)]
    [InlineData(TaskDependencyType.Sf, ProjectTaskStatus.NotStarted, false)]
    [InlineData(TaskDependencyType.Sf, ProjectTaskStatus.Blocked, true)]
    public void APredecessorMeetsTheDependencyAtThePointItsTypeNames(TaskDependencyType type, ProjectTaskStatus predecessorStatus, bool met) =>
        Assert.Equal(met, TaskDependencyRules.IsMet(type, Task(predecessorStatus)));

    /// <summary>A task blocked before it started has not started, so it holds back an SS successor.</summary>
    [Fact]
    public void ATaskBlockedBeforeItStartedHasNotStarted() =>
        Assert.False(TaskDependencyRules.IsMet(TaskDependencyType.Ss, Task(ProjectTaskStatus.Blocked, started: false)));

    [Fact]
    public void OnlyTheDependenciesOfTheStepAskedForAreUnmet()
    {
        ProjectTaskEntity designing = Task(ProjectTaskStatus.InProgress);
        ProjectTaskEntity notYet = Task(ProjectTaskStatus.NotStarted);
        ProjectTaskEntity done = Task(ProjectTaskStatus.Completed);
        ProjectTaskEntity successor = Task(ProjectTaskStatus.NotStarted);
        TaskDependency[] dependencies = [Link(designing, successor, TaskDependencyType.Fs), Link(notYet, successor, TaskDependencyType.Ff), Link(done, successor, TaskDependencyType.Fs)];
        Dictionary<Guid, ProjectTaskEntity> tasks = new[] { designing, notYet, done, successor }.ToDictionary(t => t.Id);

        Assert.Equal([designing.Id], TaskDependencyRules.Unmet(successor.Id, start: true, dependencies, tasks));
        Assert.Equal([notYet.Id], TaskDependencyRules.Unmet(successor.Id, start: false, dependencies, tasks));
    }

    /// <summary>A new dependency is refused when the successor already took the step it would gate before the predecessor got there.</summary>
    [Theory]
    [InlineData(TaskDependencyType.Fs, ProjectTaskStatus.InProgress, ProjectTaskStatus.InProgress, true)]
    [InlineData(TaskDependencyType.Fs, ProjectTaskStatus.InProgress, ProjectTaskStatus.NotStarted, false)]
    [InlineData(TaskDependencyType.Fs, ProjectTaskStatus.Completed, ProjectTaskStatus.InProgress, false)]
    [InlineData(TaskDependencyType.Ff, ProjectTaskStatus.InProgress, ProjectTaskStatus.InProgress, false)]
    [InlineData(TaskDependencyType.Ff, ProjectTaskStatus.InProgress, ProjectTaskStatus.Completed, true)]
    [InlineData(TaskDependencyType.Ss, ProjectTaskStatus.NotStarted, ProjectTaskStatus.InProgress, true)]
    public void ADependencyBrokenOnArrivalIsRecognised(TaskDependencyType type, ProjectTaskStatus predecessor, ProjectTaskStatus successor, bool broken) =>
        Assert.Equal(broken, TaskDependencyRules.IsBrokenOnArrival(type, Task(predecessor), Task(successor)));

    private static ProjectTaskEntity Task(ProjectTaskStatus status, bool? started = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = new NarrativeText("Task", Language.En),
        Status = status,
        PlannedDurationDays = 1,
        ActualStartDate = started ?? status is ProjectTaskStatus.InProgress or ProjectTaskStatus.Completed or ProjectTaskStatus.Blocked ? new DateOnly(2027, 1, 3) : null,
    };

    private static TaskDependency Link(ProjectTaskEntity predecessor, ProjectTaskEntity successor, TaskDependencyType type) =>
        new() { Id = Guid.NewGuid(), PredecessorTaskId = predecessor.Id, SuccessorTaskId = successor.Id, DependencyType = type };
}
