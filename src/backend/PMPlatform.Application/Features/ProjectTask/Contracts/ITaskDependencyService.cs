using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>The blocking dependencies between a project's tasks (TASK-048). TASK_VIEW to read them, TASK_MANAGE to change them.</summary>
public interface ITaskDependencyService
{
    public Task<TaskDependencyPage> ListDependenciesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// Links two live leaf tasks of one project. Refused if it would close a cycle, or if the successor already took the step the
    /// dependency would gate.
    /// </summary>
    public Task<AdministrationResult<Versioned<TaskDependencyDetail>>> CreateDependencyAsync(Guid callerId, TaskDependencyDraft draft, CancellationToken cancellationToken);

    /// <summary>HARD_WORKING. One that is not there is not an error (R-40).</summary>
    public Task<AdministrationError?> DeleteDependencyAsync(Guid callerId, Guid dependencyId, CancellationToken cancellationToken);
}
