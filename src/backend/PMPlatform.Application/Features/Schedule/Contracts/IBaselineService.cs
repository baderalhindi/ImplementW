using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// WF-03's baselines (TASK-046): candidates prepared from the working schedule, approved through WF-11 where the
/// governance profile requires it (ADR-015), and the single ACTIVE baseline each project has at a time, which activating
/// another supersedes atomically. Approved and superseded baselines are never changed. Access is decided on the project's
/// anchors; a baseline the caller may not see is 404 (R-47).
/// </summary>
public interface IBaselineService
{
    /// <summary>The project's baselines, latest version first.</summary>
    public Task<ProjectBaselinePage> ListAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectBaselineDetail>>> GetAsync(Guid callerId, Guid baselineId, CancellationToken cancellationToken);

    /// <summary>Opens the project's next baseline version as a DRAFT candidate; one candidate at a time.</summary>
    public Task<AdministrationResult<Versioned<ProjectBaselineDetail>>> CreateAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// DRAFT or RETURNED → SUBMITTED, starting the WF-11 run and freezing the working schedule until its outcome; or → ACTIVE
    /// at once where the project's governance profile requires no baseline approval (ADR-015).
    /// </summary>
    public Task<AdministrationResult<Versioned<ProjectBaselineDetail>>> SubmitAsync(
        Guid callerId, Guid baselineId, BaselineSubmission submission, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>HARD_DRAFT: deletes a DRAFT candidate. One that is not there is not an error (R-40).</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid baselineId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The activities the baseline froze as it activated; none before.</summary>
    public Task<AdministrationResult<BaselineActivityPage>> ListActivitiesAsync(Guid callerId, Guid baselineId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The dependencies the baseline froze as it activated; none before.</summary>
    public Task<AdministrationResult<BaselineDependencyPage>> ListDependenciesAsync(Guid callerId, Guid baselineId, PageRequest page, CancellationToken cancellationToken);
}
