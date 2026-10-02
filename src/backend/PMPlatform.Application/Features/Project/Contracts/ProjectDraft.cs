using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// The registration fields of a project, the whole set (R-5): what MOD-001 Create sends and what an edit of a DRAFT or
/// RETURNED project replaces. The Formal Project ID is not among them: AHDA issues it on approval (ADR-013).
/// </summary>
public sealed record ProjectDraft(
    NarrativeText Title,
    NarrativeText? Description,
    Guid ClassificationItemId,
    Guid DepartmentId,
    Guid? ExternalEntityId,
    ParticipationMode ParticipationMode,
    Guid GovernanceProfileItemId,
    Money? RegistrationBudgetSar,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    Guid? RegionItemId,
    Guid? CityItemId,
    decimal? Latitude,
    decimal? Longitude);
