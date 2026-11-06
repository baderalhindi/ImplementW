using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>What an accepted monthly update writes to its project (the 5 Nov 2026 review §2.2). The planned values stay as they are.</summary>
public sealed record ProjectMonthlyUpdateValues(
    decimal ActualProgressPercent, decimal? ActualSpendPercent, NarrativeText? CurrentObstacle, NarrativeText? NextAction, DateOnly? NextActionDueDate, NarrativeText? Note);

/// <summary>A project as a monthly update needs it: last month's values, which pre-fill the form, and who answers by default.</summary>
public sealed record ProjectMonthlySnapshot(
    Guid Id, string? FormalProjectId, string? LegacyReference, NarrativeText Title, Guid DepartmentId, Guid? ExternalEntityId, ProjectLifecycleState Status, Guid? EntityContactUserId,
    decimal? DeclaredPlannedPercent, decimal? DeclaredActualPercent, decimal? DeclaredPlannedSpendPercent, decimal? DeclaredActualSpendPercent, NarrativeText? CurrentObstacle,
    NarrativeText? NextAction, DateOnly? NextActionDueDate);

/// <summary>The Project module's side of the monthly update: ExternalParticipation depends on this, never on the project row.</summary>
public interface IProjectMonthlyUpdates
{
    public Task<ProjectMonthlySnapshot?> ReadAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Stages the accepted values on the project, source ENTITY_UPDATE, in the caller's unit of work. False when the project cannot take them: gone or closed.</summary>
    public Task<bool> StageAsync(Guid projectId, ProjectMonthlyUpdateValues values, Guid actorId, CancellationToken cancellationToken);

    /// <summary>Records who answers the project's update requests by default, in the caller's unit of work.</summary>
    public Task StageEntityContactAsync(Guid projectId, Guid contactUserId, Guid actorId, CancellationToken cancellationToken);
}
