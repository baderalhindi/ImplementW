using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>A row of SCR-025 Project Register, with RPMO's calculated columns (R3-3).</summary>
public sealed record ProjectSummary(
    Guid Id,
    string? FormalProjectId,
    NarrativeText Title,
    Guid ClassificationItemId,
    Guid DepartmentId,
    Guid? ExternalEntityId,
    Guid? ProjectManagerUserId,
    ProjectLifecycleState Status,
    ParticipationMode ParticipationMode,
    DateOnly? LegacyIntakeDate,
    DateTimeOffset UpdatedAt,
    string? LegacyReference,
    Guid? StrategicPortfolioItemId,
    Guid? StrategicInitiativeItemId,
    Guid? PriorityItemId,
    Guid? RegionItemId,
    Guid? CityItemId,
    Guid? ProjectImportBatchId,
    decimal? DeclaredPlannedPercent,
    decimal? DeclaredActualPercent,
    NarrativeText? CurrentObstacle,
    ProjectIndicators Indicators);
