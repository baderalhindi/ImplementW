using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Graphs;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// The blocking dependencies between a project's tasks (TASK-048). A dependency joins two live leaf tasks — a parent's progress and
/// completion follow its subtasks, so with leaves only and no cycle every task can always be reached — and is checked under
/// the project's task lock, so two concurrent links cannot each miss the other's edge.
/// </summary>
internal sealed class TaskDependencyService(
    IProjectTaskRepository repository, ProjectTaskGate gate, IAuditTrail audit, TimeProvider timeProvider) : ITaskDependencyService
{
    public async Task<TaskDependencyPage> ListDependenciesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableProjectAsync(callerId, projectId, null, cancellationToken).ConfigureAwait(false) is null)
        {
            return new TaskDependencyPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<TaskDependency> items, int total) = await repository.PageDependenciesAsync(projectId, page, cancellationToken).ConfigureAwait(false);
        return new TaskDependencyPage([.. items.Select(d => ProjectTaskMapping.ToDetail(d, projectId))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<TaskDependencyDetail>>> CreateDependencyAsync(Guid callerId, TaskDependencyDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // The project is the predecessor's. A task the caller cannot see is one that does not exist (R-47).
        ProjectTaskEntity? predecessor = await repository.FindTaskAsync(draft.PredecessorTaskId, null, cancellationToken).ConfigureAwait(false);
        Reached reached = predecessor is null
            ? new Reached(null, AdministrationError.NotFound)
            : await gate.ReachProjectAsync(callerId, PermissionCatalogue.TaskManage, predecessor.ProjectId, cancellationToken).ConfigureAwait(false);
        if (reached.Error is { Kind: AdministrationErrorKind.NotFound })
        {
            return AdministrationError.Rule(ProjectTaskErrorCodes.DependencyInvalid, new FieldIssue("predecessorTaskId", FieldIssue.NotFound));
        }

        if (reached.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = reached.Project!;
        if (ProjectTaskReferences.PlanningRefused(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IProjectTaskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        TaskBoard board = await gate.OpenAsync(project, cancellationToken).ConfigureAwait(false);
        List<FieldIssue> issues = [];
        foreach ((Guid id, string field) in new[] { (draft.PredecessorTaskId, "predecessorTaskId"), (draft.SuccessorTaskId, "successorTaskId") })
        {
            if (board.Find(id) is not { } end)
            {
                issues.Add(new FieldIssue(field, FieldIssue.NotFound));
            }
            else if (!board.IsLiveLeaf(end))
            {
                // A parent is no dependency end, and a cancelled task is out of the plan.
                issues.Add(new FieldIssue(field, FieldIssue.NotAllowed));
            }
        }

        if (draft.PredecessorTaskId == draft.SuccessorTaskId)
        {
            issues.Add(new FieldIssue("successorTaskId", FieldIssue.NotAllowed));
        }

        if (issues.Count > 0)
        {
            return AdministrationError.Rule(ProjectTaskErrorCodes.DependencyInvalid, [.. issues]);
        }

        if (board.Dependencies.Exists(d => d.PredecessorTaskId == draft.PredecessorTaskId && d.SuccessorTaskId == draft.SuccessorTaskId))
        {
            return AdministrationError.Conflict(ProjectTaskErrorCodes.DependencyExists);
        }

        if (DependencyGraph.ClosesCycle(board.Dependencies.Select(d => (d.PredecessorTaskId, d.SuccessorTaskId)), draft.PredecessorTaskId, draft.SuccessorTaskId))
        {
            return AdministrationError.Rule(ProjectTaskErrorCodes.DependencyCircular, new FieldIssue("successorTaskId", FieldIssue.NotAllowed));
        }

        if (TaskDependencyRules.IsBrokenOnArrival(draft.DependencyType, board.Find(draft.PredecessorTaskId)!, board.Find(draft.SuccessorTaskId)!))
        {
            return AdministrationError.Rule(ProjectTaskErrorCodes.DependencyUnmet, new FieldIssue("dependencyType", FieldIssue.NotAllowed));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        TaskDependency dependency = new()
        {
            Id = Guid.CreateVersion7(now),
            PredecessorTaskId = draft.PredecessorTaskId,
            SuccessorTaskId = draft.SuccessorTaskId,
            DependencyType = draft.DependencyType,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(dependency);
        board.Dependencies.Add(dependency);
        audit.Stage(ProjectTaskAudit.DependencyCreated(callerId, project, dependency));
        return await gate.SaveAsync(work, board, callerId, now, cancellationToken).ConfigureAwait(false) switch
        {
            ProjectTaskSaveOutcome.Saved => new Versioned<TaskDependencyDetail>(ProjectTaskMapping.ToDetail(dependency, project.Id), repository.RowVersionOf(dependency)),
            ProjectTaskSaveOutcome.Duplicate => AdministrationError.Conflict(ProjectTaskErrorCodes.DependencyExists),
            ProjectTaskSaveOutcome.ConcurrencyConflict => AdministrationError.PreconditionFailed,
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationError?> DeleteDependencyAsync(Guid callerId, Guid dependencyId, CancellationToken cancellationToken)
    {
        TaskDependency? dependency = await repository.FindDependencyAsync(dependencyId, cancellationToken).ConfigureAwait(false);
        ProjectTaskEntity? successor = dependency is null ? null : await repository.FindTaskAsync(dependency.SuccessorTaskId, null, cancellationToken).ConfigureAwait(false);
        if (successor is null)
        {
            return AdministrationError.NotFound;
        }

        Reached reached = await gate.ReachProjectAsync(callerId, PermissionCatalogue.TaskManage, successor.ProjectId, cancellationToken).ConfigureAwait(false);
        if (reached.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = reached.Project!;
        if (ProjectTaskReferences.PlanningRefused(project) is { } notEligible)
        {
            return notEligible;
        }

        await using IProjectTaskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        TaskBoard board = await gate.OpenAsync(project, cancellationToken).ConfigureAwait(false);
        board.Dependencies.RemoveAll(d => d.Id == dependencyId);
        repository.Remove(dependency!);
        DateTimeOffset now = timeProvider.GetUtcNow();
        audit.Stage(ProjectTaskAudit.DependencyDeleted(callerId, project, dependency!));

        // A dependency is never updated, so a concurrency conflict is a concurrent delete: gone either way (R-40).
        return await gate.SaveAsync(work, board, callerId, now, cancellationToken).ConfigureAwait(false) == ProjectTaskSaveOutcome.Saved ? null : AdministrationError.NotFound;
    }
}
