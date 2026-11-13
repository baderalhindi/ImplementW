using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// What an accepted monthly update writes to its project (the 5 Nov 2026 review §2.2). The planned values stay as they are, unless the answer gives the
/// current stage (review round 7, item 1): then the overall figure is weighed from the stage plan — the stages before it complete, it at its %, the
/// rest not started — and the planned figure is the plan's today.
/// </summary>
public sealed record ProjectMonthlyUpdateValues(
    decimal? ActualProgressPercent, decimal? ActualSpendPercent, NarrativeText? CurrentObstacle, NarrativeText? NextAction, DateOnly? NextActionDueDate, NarrativeText? Note,
    string? CurrentStageCode = null, decimal? CurrentStagePercent = null);

/// <summary>A stage of the project's plan that applies, as the monthly update shows it: its name, weight and planned % today.</summary>
public sealed record ProjectMonthlyStage(string StageCode, BilingualLabel Name, decimal WeightPercent, decimal? PlannedPercent);

/// <summary>A published period's figures for its project: the actual, and for a stage-based period the planned figure and the current stage with its %.</summary>
public sealed record PublishedProgressValues(decimal ActualPercent, bool IsStageBased = false, decimal? PlannedPercent = null, string? CurrentStageCode = null, decimal? CurrentStagePercent = null);

/// <summary>A project as a monthly update needs it: last month's values, which pre-fill the form, and who answers by default.</summary>
public sealed record ProjectMonthlySnapshot(
    Guid Id, string? FormalProjectId, string? LegacyReference, NarrativeText Title, Guid DepartmentId, Guid? ExternalEntityId, ProjectLifecycleState Status, Guid? EntityContactUserId,
    decimal? DeclaredPlannedPercent, decimal? DeclaredActualPercent, decimal? DeclaredPlannedSpendPercent, decimal? DeclaredActualSpendPercent, NarrativeText? CurrentObstacle,
    NarrativeText? NextAction, DateOnly? NextActionDueDate)
{
    /// <summary>The stages that apply, in stage order; empty when the project has no stage plan or no stages are configured.</summary>
    public IReadOnlyList<ProjectMonthlyStage> Stages { get; init; } = [];

    public string? CurrentStageCode { get; init; }

    public decimal? CurrentStagePercent { get; init; }
}

/// <summary>The Project module's side of the monthly update: ExternalParticipation depends on this, never on the project row.</summary>
public interface IProjectMonthlyUpdates
{
    public Task<ProjectMonthlySnapshot?> ReadAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Stages the accepted values on the project, source ENTITY_UPDATE, in the caller's unit of work. False when the project cannot take them: gone or closed.</summary>
    public Task<bool> StageAsync(Guid projectId, ProjectMonthlyUpdateValues values, Guid actorId, CancellationToken cancellationToken);

    /// <summary>
    /// Stages a published progress figure as the project's declared actual, source PROGRESS_UPDATE, in the caller's unit of work. A stage-based period
    /// (review round 7, item 1) also gives its planned figure and current stage, which replace the declared ones.
    /// </summary>
    public Task StagePublishedProgressAsync(Guid projectId, PublishedProgressValues values, Guid actorId, CancellationToken cancellationToken);

    /// <summary>Records who answers the project's update requests by default, in the caller's unit of work.</summary>
    public Task StageEntityContactAsync(Guid projectId, Guid contactUserId, Guid actorId, CancellationToken cancellationToken);
}
