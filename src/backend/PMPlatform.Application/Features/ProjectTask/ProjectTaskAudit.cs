using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>The audit events of WF-04 (TASK-033 <c>IAuditTrail</c>). Titles, descriptions and reasons are free text and are not copied.</summary>
internal static class ProjectTaskAudit
{
    public const string Module = "ProjectTask";
    public const string TaskType = "ProjectTask";
    public const string DependencyType = "TaskDependency";
    public const string ProgressType = "ActivityExecutionProgress";

    public static AuditEntry Created(Guid actorId, ProjectFacts project, ProjectTaskEntity task) =>
        Entry(AuditEventClass.DataChange, ProjectTaskAuditEvents.TaskCreated, actorId, project, SubjectOf(task),
        [
            AuditAttribute.Of(ProjectTaskAuditAttributes.ParentTaskId, task.ParentTaskId),
            AuditAttribute.Of(ProjectTaskAuditAttributes.ScheduleActivityId, task.ScheduleActivityId),
            AuditAttribute.Of(ProjectTaskAuditAttributes.AssigneeUserId, task.AssigneeUserId),
            AuditAttribute.Of(ProjectTaskAuditAttributes.PriorityItemId, task.PriorityItemId),
            AuditAttribute.Of(ProjectTaskAuditAttributes.PlannedStartDate, task.PlannedStartDate),
            AuditAttribute.Of(ProjectTaskAuditAttributes.PlannedFinishDate, task.PlannedFinishDate),
            AuditAttribute.Of(ProjectTaskAuditAttributes.PlannedDurationDays, task.PlannedDurationDays),
        ]);

    public static AuditEntry Changed(Guid actorId, ProjectFacts project, TaskPlan before, ProjectTaskEntity task) =>
        Entry(AuditEventClass.DataChange, ProjectTaskAuditEvents.TaskChanged, actorId, project, SubjectOf(task),
            new[]
            {
                AuditAttribute.Change(ProjectTaskAuditAttributes.ScheduleActivityId, before.ScheduleActivityId, task.ScheduleActivityId),
                AuditAttribute.WithheldChange(ProjectTaskAuditAttributes.Title, before.Title.Text, task.Title.Text),
                AuditAttribute.WithheldChange(ProjectTaskAuditAttributes.Description, before.Description?.Text, task.Description?.Text),
                AuditAttribute.Change(ProjectTaskAuditAttributes.AssigneeUserId, before.AssigneeUserId, task.AssigneeUserId),
                AuditAttribute.Change(ProjectTaskAuditAttributes.PriorityItemId, before.PriorityItemId, task.PriorityItemId),
                AuditAttribute.Change(ProjectTaskAuditAttributes.PlannedStartDate, before.PlannedStartDate, task.PlannedStartDate),
                AuditAttribute.Change(ProjectTaskAuditAttributes.PlannedFinishDate, before.PlannedFinishDate, task.PlannedFinishDate),
                AuditAttribute.Change(ProjectTaskAuditAttributes.PlannedDurationDays, before.PlannedDurationDays, task.PlannedDurationDays),
            }.OfType<AuditAttribute>());

    public static AuditEntry ProgressReported(Guid actorId, ProjectFacts project, decimal? before, ProjectTaskEntity task) =>
        Entry(AuditEventClass.DataChange, ProjectTaskAuditEvents.TaskProgressReported, actorId, project, SubjectOf(task),
        [
            AuditAttribute.Change(ProjectTaskAuditAttributes.ActualPercentComplete, before, task.ActualPercentComplete)
                ?? AuditAttribute.Of(ProjectTaskAuditAttributes.ActualPercentComplete, task.ActualPercentComplete),
            AuditAttribute.Of(ProjectTaskAuditAttributes.AssigneeUserId, task.AssigneeUserId),
        ]);

    /// <summary>A move along <see cref="ProjectTaskWorkflow"/>, with the dates and counters it set.</summary>
    public static AuditEntry Transition(string eventType, Guid actorId, ProjectFacts project, ProjectTaskStatus from, ProjectTaskEntity task) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, SubjectOf(task),
        [
            AuditAttribute.Change(ProjectTaskAuditAttributes.Status, from, task.Status)!,
            AuditAttribute.Of(ProjectTaskAuditAttributes.ActualStartDate, task.ActualStartDate),
            AuditAttribute.Of(ProjectTaskAuditAttributes.ActualFinishDate, task.ActualFinishDate),
            AuditAttribute.Of(ProjectTaskAuditAttributes.ActualPercentComplete, task.ActualPercentComplete),
            AuditAttribute.Of(ProjectTaskAuditAttributes.ReopenedCount, task.ReopenedCount),
            AuditAttribute.Of(ProjectTaskAuditAttributes.BlockedReason, task.BlockedReason is null ? null : AuditAttribute.Withheld),
        ]);

    public static AuditEntry DependencyCreated(Guid actorId, ProjectFacts project, TaskDependency dependency) =>
        Entry(AuditEventClass.DataChange, ProjectTaskAuditEvents.DependencyCreated, actorId, project, SubjectOf(dependency), DependencyAttributes(dependency));

    public static AuditEntry DependencyDeleted(Guid actorId, ProjectFacts project, TaskDependency dependency) =>
        Entry(AuditEventClass.DataChange, ProjectTaskAuditEvents.DependencyDeleted, actorId, project, SubjectOf(dependency), DependencyAttributes(dependency));

    public static AuditEntry ProgressRecomputed(Guid actorId, ProjectFacts project, ActivityExecutionProgress progress, decimal? before) =>
        Entry(AuditEventClass.DataChange, ProjectTaskAuditEvents.ActivityProgressRecomputed, actorId, project, new AuditSubject(Module, ProgressType, progress.Id),
        [
            AuditAttribute.Of(ProjectTaskAuditAttributes.ScheduleActivityId, progress.ScheduleActivityId),
            AuditAttribute.Change(ProjectTaskAuditAttributes.ActualPercentComplete, before, progress.ActualPercentComplete)
                ?? AuditAttribute.Of(ProjectTaskAuditAttributes.ActualPercentComplete, progress.ActualPercentComplete),
        ]);

    private static AuditAttribute[] DependencyAttributes(TaskDependency dependency) =>
    [
        AuditAttribute.Of(ProjectTaskAuditAttributes.PredecessorTaskId, dependency.PredecessorTaskId),
        AuditAttribute.Of(ProjectTaskAuditAttributes.SuccessorTaskId, dependency.SuccessorTaskId),
        AuditAttribute.Of(ProjectTaskAuditAttributes.DependencyType, dependency.DependencyType),
    ];

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, ProjectFacts project, AuditSubject subject, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = subject,
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes],
        };

    private static AuditSubject SubjectOf(ProjectTaskEntity task) => new(Module, TaskType, task.Id);

    private static AuditSubject SubjectOf(TaskDependency dependency) => new(Module, DependencyType, dependency.Id);
}

/// <summary>A task's plan before a change, for its audit event.</summary>
internal sealed record TaskPlan(
    Guid? ScheduleActivityId, NarrativeText Title, NarrativeText? Description, Guid? AssigneeUserId, Guid? PriorityItemId,
    DateOnly PlannedStartDate, DateOnly PlannedFinishDate, int PlannedDurationDays)
{
    public static TaskPlan Of(ProjectTaskEntity t) =>
        new(t.ScheduleActivityId, t.Title, t.Description, t.AssigneeUserId, t.PriorityItemId, t.PlannedStartDate, t.PlannedFinishDate, t.PlannedDurationDays);
}
