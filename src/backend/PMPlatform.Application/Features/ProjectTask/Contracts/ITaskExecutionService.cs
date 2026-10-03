using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>
/// A task's execution along its state machine (TASK-048), while the project is ACTIVE: NOT_STARTED → IN_PROGRESS ⇄ BLOCKED →
/// COMPLETED, and the controlled reopen and cancel. Start and completion wait for the task's dependencies. Each command takes
/// the caller's version when they send one (R-21) and recalculates the Activity Execution Progress it moves.
/// </summary>
public interface ITaskExecutionService
{
    /// <summary>NOT_STARTED → IN_PROGRESS, once every FS and SS predecessor allows it. TASK_UPDATE.</summary>
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> StartAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>NOT_STARTED or IN_PROGRESS → BLOCKED, with the reason. TASK_UPDATE.</summary>
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> BlockAsync(
        Guid callerId, Guid taskId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>BLOCKED → where it was blocked from: IN_PROGRESS once started, NOT_STARTED otherwise. TASK_UPDATE.</summary>
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> UnblockAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>IN_PROGRESS → COMPLETED, once every FF and SF predecessor and every live subtask allows it. A BLOCKED task is unblocked first. TASK_UPDATE.</summary>
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> CompleteAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>COMPLETED → IN_PROGRESS. TASK_REOPEN only: general edit permission does not reopen a task.</summary>
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> ReopenAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>A live task → CANCELLED, final, when it has no live subtask and no dependency; it leaves every roll-up. TASK_MANAGE.</summary>
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> CancelAsync(Guid callerId, Guid taskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// A leaf's actual percentage, 0–100, while it is IN_PROGRESS or BLOCKED: maintained by its owner and editable by the Project
    /// Manager (ADR-009). TASK_UPDATE.
    /// </summary>
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> ReportProgressAsync(
        Guid callerId, Guid taskId, decimal actualPercentComplete, uint? expectedVersion, CancellationToken cancellationToken);
}
