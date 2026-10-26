using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// WF-03's side of the shared milestone (ICD-04, TASK-050): the schedule representation of a project's milestones — title,
/// category, the activity each completes, the Current Forecast date — and their PLANNED or CANCELLED status. Their
/// achievement is WF-05's. Each operation is decided by the authorization engine on the project's anchors; a collection of a
/// project the caller may not see is empty, and a milestone they may not see is 404 (R-47).
/// </summary>
public interface IProjectMilestoneService
{
    /// <summary>The project's milestones, earliest forecast first, with the ACTIVE baseline's date and the variance from it.</summary>
    public Task<ProjectMilestonePage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectMilestoneDetail>>> GetAsync(Guid callerId, Guid milestoneId, CancellationToken cancellationToken);

    /// <summary>Adds a PLANNED milestone to the project's schedule.</summary>
    public Task<AdministrationResult<Versioned<ProjectMilestoneDetail>>> CreateAsync(Guid callerId, ProjectMilestoneDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces a PLANNED milestone's inputs. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<ProjectMilestoneDetail>>> UpdateAsync(
        Guid callerId, Guid milestoneId, ProjectMilestoneChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>PLANNED → CANCELLED: the milestone leaves the plan, and no achievement of it can be accepted any more.</summary>
    public Task<AdministrationResult<Versioned<ProjectMilestoneDetail>>> CancelAsync(Guid callerId, Guid milestoneId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The milestones a baseline froze as it activated.</summary>
    public Task<AdministrationResult<BaselineMilestonePage>> ListBaselineMilestonesAsync(Guid callerId, Guid baselineId, PageRequest page, CancellationToken cancellationToken);
}
