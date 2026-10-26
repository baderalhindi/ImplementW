using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// WF-03's working schedule (TASK-046): the work breakdown, its dependencies, the Current Forecast, and the variance and
/// Schedule Health measured against the ACTIVE baseline. Dates are calculated by the backend after every change. Each
/// operation is decided by the authorization engine on the project's anchors. A collection is of one project and is empty
/// for a project the caller may not see; an activity or dependency the caller may not see is 404 (R-47).
/// </summary>
public interface IScheduleService
{
    public Task<ProjectSchedulePage> ListSchedulesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Creates the project's schedule, once, while the project is APPROVED_PLANNED or ACTIVE.</summary>
    public Task<AdministrationResult<Versioned<ProjectScheduleDetail>>> InitializeAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>The activities in work-breakdown order, with their baseline dates and variance.</summary>
    public Task<ScheduleActivityPage> ListActivitiesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> GetActivityAsync(Guid callerId, Guid activityId, CancellationToken cancellationToken);

    /// <summary>Adds an activity and recalculates the schedule. A parent becomes a summary.</summary>
    public Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> CreateActivityAsync(Guid callerId, ScheduleActivityDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces an activity's inputs and recalculates the schedule. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> UpdateActivityAsync(
        Guid callerId, Guid activityId, ScheduleActivityChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>PLANNED → CANCELLED: the activity leaves the plan and the forecast; no baseline is touched.</summary>
    public Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> CancelActivityAsync(Guid callerId, Guid activityId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Sets a leaf's Current Forecast while the project has an ACTIVE baseline; the baseline never moves.</summary>
    public Task<AdministrationResult<Versioned<ScheduleActivityDetail>>> ReforecastActivityAsync(
        Guid callerId, Guid activityId, ScheduleForecast forecast, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<ScheduleDependencyPage> ListDependenciesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Adds a dependency, refused if it would close a cycle, and recalculates the schedule.</summary>
    public Task<AdministrationResult<Versioned<ScheduleDependencyDetail>>> CreateDependencyAsync(Guid callerId, ScheduleDependencyDraft draft, CancellationToken cancellationToken);

    /// <summary>HARD_WORKING: removes a dependency and recalculates the schedule. One that is not there is not an error (R-40).</summary>
    public Task<AdministrationError?> DeleteDependencyAsync(Guid callerId, Guid dependencyId, CancellationToken cancellationToken);

    /// <summary>The project's CURRENT/LIVE Schedule Health.</summary>
    public Task<ScheduleHealthStatusPage> ListHealthStatusesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);
}
