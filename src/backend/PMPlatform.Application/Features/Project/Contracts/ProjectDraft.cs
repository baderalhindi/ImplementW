using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// The registration fields of a project, the whole set (R-5): what MOD-001 Create sends and what an edit of a DRAFT or
/// RETURNED project replaces. The Formal Project ID is not among them: AHDA issues it on approval (ADR-013). The strategy classification —
/// strategic portfolio, then one of its initiatives — priority, funding source, legacy reference and follow-up owner came with the 2 Nov 2026
/// review (R3-3); a portfolio and an initiative are a view of the project, never a scope.
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
    decimal? Longitude,
    Guid? StrategicPortfolioItemId = null,
    Guid? StrategicInitiativeItemId = null,
    Guid? PriorityItemId = null,
    FundingSource? FundingSource = null,
    string? LegacyReference = null,
    Guid? FollowUpOwnerUserId = null);
