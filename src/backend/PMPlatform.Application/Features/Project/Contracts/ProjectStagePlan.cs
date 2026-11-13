using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// One stage of a project's stage plan as read (review round 7, item 1): the stage as the PROJECT_STAGE version in force names and weighs it,
/// and the project's plan for it. A stage the project has no row for yet is pre-filled: it applies, with no dates.
/// </summary>
public sealed record ProjectStagePlanStage(
    string StageCode, BilingualLabel Name, short SortOrder, decimal WeightPercent, bool Applies, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate,
    decimal? StartingPercent);

/// <summary>
/// A project's stage plan. <see cref="RevisionNo"/> is 0 until the plan is first saved. <see cref="RequiresChangeAuthorization"/>: a change now claims an
/// approved schedule change request's STAGE_PLAN_CHANGE authorisation. <see cref="AcceptsStartingPercent"/>: the project was already under way when it
/// entered the platform, so each stage may carry how far it had come. <see cref="StageVersionNo"/> is the PROJECT_STAGE version in force; null when
/// none is published, and then the project has no stages to plan.
/// </summary>
public sealed record ProjectStagePlan(
    Guid ProjectId, int RevisionNo, bool RequiresChangeAuthorization, bool AcceptsStartingPercent, Guid? StageVersionId, int? StageVersionNo,
    IReadOnlyList<ProjectStagePlanStage> Stages);

public sealed record ProjectStagePage(IReadOnlyList<ProjectStagePlanStage> Items, int Page, int PageSize, int TotalCount);

/// <summary>One stage as written. The dates are required while it applies, and are ignored with the starting % when it does not.</summary>
public sealed record ProjectStagePlanEntryChange(string StageCode, bool Applies, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, decimal? StartingPercent);

/// <summary>Every stage of the version in force, and, once the registration is approved, the authorisation the change claims and why.</summary>
public sealed record ProjectStagePlanChanges(IReadOnlyList<ProjectStagePlanEntryChange> Stages, Guid? ChangeAuthorizationId, NarrativeText? Reason);

/// <summary>A project's stage plan (review round 7, item 1): set at registration and reviewed with it; changed after approval only by an approved schedule change.</summary>
public interface IProjectStagePlanService
{
    public Task<AdministrationResult<Versioned<ProjectStagePlan>>> GetAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>The stages of the PROJECT_STAGE version in force, in stage order, as a new registration's plan starts: each applying, undated. None when none is published.</summary>
    public Task<ProjectStagePage> ListStagesAsync(PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the plan: freely while the registration is DRAFT or RETURNED, once on a project that has none (one registered before stages, or
    /// imported), and otherwise only with an applicable STAGE_PLAN_CHANGE authorisation of the plan's revision. Requires the project's version (R-21).
    /// </summary>
    public Task<AdministrationResult<Versioned<ProjectStagePlan>>> ReplaceAsync(
        Guid callerId, Guid projectId, ProjectStagePlanChanges changes, uint expectedVersion, CancellationToken cancellationToken);
}

/// <summary>A stage of the plan as another module needs it: what applies, and when.</summary>
public sealed record ProjectStagePlanFact(string StageCode, bool Applies, DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, decimal? StartingPercent);

/// <summary>The Project module's stage plans for the modules that weigh progress by stage (Progress, Dashboards, ExternalParticipation, Reports).</summary>
public interface IProjectStagePlans
{
    public Task<IReadOnlyList<ProjectStagePlanFact>> ReadAsync(Guid projectId, CancellationToken cancellationToken);
}

/// <summary>
/// The port through which a change to an approved project's stage plan applies the STAGE_PLAN_CHANGE authorisation an approved schedule change issued
/// (review round 7, item 1). ChangeRequest implements it over its own authorisations (ADR-003 §8.2 edge 6, the direction that already exists), so
/// Project references no module back and the graph stays acyclic.
/// </summary>
public interface IStagePlanChangeAuthorization
{
    /// <summary>
    /// Applies the authorisation to the plan at <paramref name="revisionNo"/>, in the caller's unit of work: true only when it was issued for this
    /// project's stage plan at that revision and its change request is in implementation.
    /// </summary>
    public Task<bool> ApplyAsync(Guid changeAuthorizationId, Guid projectId, int revisionNo, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
}
