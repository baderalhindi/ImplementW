using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>A row of SCR-025 Project Register.</summary>
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
    DateTimeOffset UpdatedAt);
