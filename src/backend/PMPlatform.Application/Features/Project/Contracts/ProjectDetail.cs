using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// A project as SCR-026 shows it. <see cref="FormalProjectId"/> is null until AHDA approves the registration.
/// <see cref="LegacyIntakeDate"/> is set only on a project that entered by the legacy intake path (ADR-014, TASK-104): its
/// baseline is a Declared Baseline, never an Approved one, so every display can tell the two apart. <see cref="ClosedAt"/> is when WF-10's
/// closure took effect (TASK-063); a closed project's completion date, or its having been terminated without one, is its closure cases'.
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
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    Guid? StrategicPortfolioItemId,
    Guid? StrategicInitiativeItemId,
    Guid? PriorityItemId,
    FundingSource? FundingSource,
    string? LegacyReference,
    Guid? FollowUpOwnerUserId,
    Guid? ProjectImportBatchId,
    ProjectFollowUpDetail FollowUp,
    ProjectIndicators Indicators,
    string? TitleEn = null,
    bool? IsStrategic = null,
    Guid? EntityContactUserId = null,
    bool IsCanceled = false)
{
    /// <summary>The name of <see cref="RegistrationBudgetSar"/> in <see cref="MaskedFields"/>.</summary>
    public const string RegistrationBudgetSarField = "registrationBudgetSar";

    /// <summary>The Project Manager the registration form names (review round 7, item 2); the submission proposes them.</summary>
    public Guid? ProposedProjectManagerUserId { get; init; }

    /// <summary>
    /// The fields withheld from the caller (ADR-010, R-20): null here, omitted by the API. The registration budget is a SAR amount, so a caller
    /// without FINANCIAL_VIEW_SENSITIVE on the project gets it withheld, as the financial screens and dashboards withhold theirs (D-4).
    /// </summary>
    public IReadOnlyList<string> MaskedFields { get; init; } = [];
}
