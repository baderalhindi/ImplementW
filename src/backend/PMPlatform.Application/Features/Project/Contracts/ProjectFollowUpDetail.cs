using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// A project's follow-up as RPMO keeps it (R3-3). Every value is optional: a blank is "not available", never zero. The declared values hold
/// as of <see cref="DeclaredAsOf"/>, from <see cref="DeclaredSource"/>, until the project has a schedule to calculate them from.
/// </summary>
public sealed record ProjectFollowUpDetail(
    NarrativeText? CurrentObstacle,
    NarrativeText? NextAction,
    string? NextActionOwner,
    DateOnly? NextActionDueDate,
    decimal? DeclaredPlannedPercent,
    decimal? DeclaredActualPercent,
    decimal? DeclaredPlannedSpendPercent,
    decimal? DeclaredActualSpendPercent,
    DateOnly? DeclaredAsOf,
    DeclaredValueSource? DeclaredSource,
    bool? EntityUpdateCompliant);

/// <summary>
/// The whole follow-up, as the follow-up command replaces it (R-5). <see cref="Note"/>, when given, is added as a dated follow-up note in
/// the same change.
/// </summary>
public sealed record ProjectFollowUpChanges(
    NarrativeText? CurrentObstacle,
    NarrativeText? NextAction,
    string? NextActionOwner,
    DateOnly? NextActionDueDate,
    decimal? DeclaredPlannedPercent,
    decimal? DeclaredActualPercent,
    decimal? DeclaredPlannedSpendPercent,
    decimal? DeclaredActualSpendPercent,
    DateOnly? DeclaredAsOf,
    bool? EntityUpdateCompliant,
    NarrativeText? Note);

/// <summary>A dated follow-up note, attributed to who wrote it.</summary>
public sealed record ProjectFollowUpNoteDetail(Guid Id, Guid ProjectId, NarrativeText Note, DateTimeOffset RecordedAt, Guid RecordedBy);

/// <summary>One row of the Follow-up list (قائمة المتابعة): a project that needs following up, why, and what is next.</summary>
public sealed record ProjectFollowUpItem(
    Guid ProjectId,
    string? FormalProjectId,
    string? LegacyReference,
    NarrativeText Title,
    ProjectLifecycleState Status,
    Guid DepartmentId,
    Guid? ExternalEntityId,
    Guid? FollowUpOwnerUserId,
    NarrativeText? CurrentObstacle,
    NarrativeText? NextAction,
    string? NextActionOwner,
    DateOnly? NextActionDueDate,
    ProjectIndicators Indicators);
