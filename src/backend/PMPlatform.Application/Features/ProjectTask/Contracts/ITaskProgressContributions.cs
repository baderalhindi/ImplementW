using PMPlatform.Domain.Common;
using PMPlatform.Domain.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 19, ExternalParticipation → ProjectTask (command; TASK-066): WF-13 applies an entity's accepted, AHDA-reviewed
/// report of a task's actual percentage — the one allowlisted field of WF-04 an external answer may set (WF-13 UPDATE_ALLOWED_SOURCE_FIELDS).
/// WF-04 stays the authority: it revalidates the task under the project's task lock, refuses a task that changed since the answer was
/// given, applies its own rules for entering a percentage (ADR-009), rolls the Activity Execution Progress up again and audits the change
/// as its own, naming the external lineage. The authority is the accepted contribution WF-13 names, not a permission of the caller, so this
/// is not on <see cref="ITaskExecutionService"/>.
/// </summary>
public interface ITaskProgressContributions
{
    /// <summary>The task as it is now, with its row version; null when there is no such task. Not tracked.</summary>
    public Task<TaskProgressSource?> FindAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Stages the percentage in the caller's unit of work, so it commits with the caller's records or not at all (M-11). Takes the project's
    /// task lock first; nothing is staged unless the outcome is <see cref="TaskProgressApplicationOutcome.Staged"/>.
    /// </summary>
    public Task<TaskProgressApplication> StageAsync(TaskProgressContribution contribution, CancellationToken cancellationToken);
}

/// <summary>A task's identity, project, state and row version, and its title as a safe label for the entity answering about it.</summary>
public sealed record TaskProgressSource(Guid TaskId, Guid ProjectId, ProjectTaskStatus Status, uint Version, NarrativeText Title);

/// <summary>The percentage to enter, the row version the answer was given against, and who applies it with what lineage.</summary>
public sealed record TaskProgressContribution(Guid TaskId, uint ExpectedVersion, decimal ActualPercentComplete, Guid ActorId, ExternalContributionLineage Lineage);

/// <summary>WF-13 BR-EXT-025: the external origin an applied value keeps — the entity, the contribution revision and the application attempt.</summary>
public sealed record ExternalContributionLineage(Guid ExternalEntityId, Guid ExternalContributionId, int RevisionNo, Guid SourceApplicationId);

/// <summary>The outcome, and the task's row version as found when it was not the expected one.</summary>
public sealed record TaskProgressApplication(TaskProgressApplicationOutcome Outcome, uint? CurrentVersion = null);

public enum TaskProgressApplicationOutcome
{
    /// <summary>Entered, rolled up and audited in the caller's unit of work.</summary>
    Staged = 1,

    /// <summary>No such task.</summary>
    TaskNotFound = 2,

    /// <summary>The task is CANCELLED and takes no percentage again.</summary>
    TaskCancelled = 3,

    /// <summary>The task changed since the answer was given: nothing is overwritten (WF-13 EXT-P-09).</summary>
    VersionChanged = 4,

    /// <summary>The project is not ACTIVE, so no task executes (WF-04 D-9).</summary>
    ProjectNotEligible = 5,

    /// <summary>The task is not a leaf IN_PROGRESS or BLOCKED, so WF-04 takes no percentage for it now (ADR-009, WF-04 D-7).</summary>
    ProgressNotEnterable = 6,
}
