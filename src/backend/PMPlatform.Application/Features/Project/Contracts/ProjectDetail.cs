using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// A project as SCR-026 shows it. <see cref="FormalProjectId"/> is null until AHDA approves the registration.
/// <see cref="LegacyIntakeDate"/> is set only on a project that entered by the legacy intake path (ADR-014, TASK-104): its
/// baseline is a Declared Baseline, never an Approved one, so every display can tell the two apart.
/// </summary>
public sealed record ProjectDetail(
    Guid Id,
    string? FormalProjectId,
    NarrativeText Title,
    NarrativeText? Description,
    Guid ClassificationItemId,
    Guid DepartmentId,
    Guid? ExternalEntityId,
    Guid? ProjectManagerUserId,
    ProjectLifecycleState Status,
    int RevisionNo,
    Guid GovernanceProfileItemId,
    ParticipationMode ParticipationMode,
    Money? RegistrationBudgetSar,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    Guid? RegionItemId,
    Guid? CityItemId,
    decimal? Latitude,
    decimal? Longitude,
    DateOnly? LegacyIntakeDate,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
