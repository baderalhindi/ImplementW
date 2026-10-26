using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// What a task may name and when its project admits it: its schedule activity (WF-03, edge 9), its owner (IdentityAccess, E-U1),
/// its priority (MasterDataConfig, E-U2), and the project's lifecycle state (Project, edge 36).
/// </summary>
internal sealed class ProjectTaskReferences(
    IScheduleActivityReader activities, IRoleDirectory roles, IRoleHolderDirectory holders, IMasterDataResolver masterData)
{
    public const string PriorityCatalogue = "PRIORITY";

    /// <summary>Tasks are planned while the project is APPROVED_PLANNED or ACTIVE, as its schedule is.</summary>
    public static AdministrationError? PlanningRefused(ProjectFacts project) =>
        project.Status is ProjectLifecycleState.ApprovedPlanned or ProjectLifecycleState.Active ? null : AdministrationError.Rule(ProjectTaskErrorCodes.ProjectNotEligible);

    /// <summary>Tasks are executed while the project is ACTIVE: not before activation, and not while it is suspended or closing.</summary>
    public static AdministrationError? ExecutionRefused(ProjectFacts project) =>
        project.Status == ProjectLifecycleState.Active ? null : AdministrationError.Rule(ProjectTaskErrorCodes.ProjectNotEligible);

    /// <summary>The first reference of a task's plan that does not hold, as its refusal; null when they all hold.</summary>
    public async Task<AdministrationError?> CheckAsync(ProjectFacts project, Guid? scheduleActivityId, Guid? assigneeUserId, Guid? priorityItemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (scheduleActivityId is { } activityId && !await IsLiveLeafOfAsync(project.Id, activityId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Rule(ProjectTaskErrorCodes.ScheduleActivityInvalid, new FieldIssue("scheduleActivityId", FieldIssue.NotFound));
        }

        if (assigneeUserId is { } assignee && !await HoldsARoleOverAsync(assignee, project, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Rule(ProjectTaskErrorCodes.AssigneeNotEligible, new FieldIssue("assigneeUserId", FieldIssue.NotAllowed));
        }

        if (priorityItemId is { } priority)
        {
            try
            {
                await masterData.RequirePublishedItemAsync(PriorityCatalogue, priority, cancellationToken).ConfigureAwait(false);
            }
            catch (ConfigurationMissingException)
            {
                return AdministrationError.Rule(ProjectTaskErrorCodes.PriorityInvalid, new FieldIssue("priorityItemId", FieldIssue.NotFound));
            }
        }

        return null;
    }

    /// <summary>A task executes against a live leaf activity of its own project's schedule; a summary's progress is rolled up by WF-02.</summary>
    private async Task<bool> IsLiveLeafOfAsync(Guid projectId, Guid activityId, CancellationToken cancellationToken) =>
        await activities.FindAsync(activityId, cancellationToken).ConfigureAwait(false) is { } activity
        && activity.ProjectId == projectId
        && activity.ActivityKind == ScheduleActivityKind.Activity
        && activity.Status != ScheduleActivityStatus.Cancelled;

    /// <summary>
    /// The ERD's "must hold a project relationship": the owner holds some role over the project's anchors now — an internal
    /// holder, or an external one of the delivering entity itself (ADR-013), as IdentityAccess decides it.
    /// </summary>
    private async Task<bool> HoldsARoleOverAsync(Guid userId, ProjectFacts project, CancellationToken cancellationToken)
    {
        Guid[] roleIds = [.. (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).Select(r => r.Id)];
        RoleHolderScope scope = new(project.Id, project.DepartmentId, project.ExternalEntityId);
        return await holders.FindHolderAsync(userId, roleIds, scope, cancellationToken).ConfigureAwait(false) is not null;
    }
}
