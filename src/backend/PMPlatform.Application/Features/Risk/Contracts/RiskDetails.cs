using PMPlatform.Domain.Common;
using PMPlatform.Domain.Risk;

namespace PMPlatform.Application.Features.Risk.Contracts;

/// <summary>
/// A risk register entry. <see cref="CurrentAssessment"/> is its latest assessment, whose rating was fixed by the matrix version
/// that assessment pinned; null while the risk was never assessed. <see cref="AcceptedUntil"/> is the expiry of its ACTIVE
/// acceptance, if it has one. <see cref="MaterialisedIssueIds"/> are the issues WF-07 raised from it (edge 15), each holding the
/// risk as its <c>originating_risk_id</c>: the risk side of the linkage.
/// </summary>
public sealed record RiskDetail(
    Guid Id,
    Guid ProjectId,
    NarrativeText Title,
    NarrativeText Description,
    Guid RiskCategoryItemId,
    Guid? OwnerUserId,
    RiskStatus Status,
    DateOnly IdentifiedDate,
    DateOnly? NextReviewDate,
    RiskAssessmentSummary? CurrentAssessment,
    DateOnly? AcceptedUntil,
    DateTimeOffset? MaterialisedAt,
    IReadOnlyList<Guid> MaterialisedIssueIds,
    NarrativeText? ClosureRationale,
    DateTimeOffset? ClosedAt,
    Guid? ClosedByUserId,
    int ReopenedCount,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>A rating as the pinned RISK_MATRIX version defines it (ADR-011; labels outstanding, OQ-006).</summary>
public sealed record RiskRatingDetail(Guid Id, string Code, BilingualLabel Label);

/// <summary>The figures of an assessment that decide its rating.</summary>
public sealed record RiskAssessmentSummary(
    Guid Id, int VersionNo, DateTimeOffset AssessedAt, Guid MatrixConfigurationVersionId, short ProbabilityLevel, short OverallImpactLevel, RiskRatingDetail Rating);

/// <summary>
/// One assessment version, whole: its probability, its impact per dimension, the overall impact (the highest of them) and the
/// rating, all as recorded against <see cref="MatrixConfigurationVersionId"/>. Nothing in it changes when the matrix is
/// published again.
/// </summary>
public sealed record RiskAssessmentDetail(
    Guid Id,
    Guid RiskId,
    int VersionNo,
    DateTimeOffset AssessedAt,
    Guid AssessedByUserId,
    Guid MatrixConfigurationVersionId,
    short ProbabilityLevel,
    short OverallImpactLevel,
    RiskRatingDetail Rating,
    IReadOnlyList<RiskAssessmentImpactDetail> Impacts,
    NarrativeText? Rationale);

public sealed record RiskAssessmentImpactDetail(Guid ImpactDimensionItemId, short ImpactLevel, NarrativeText? Rationale);

public sealed record RiskTreatmentActionDetail(
    Guid Id,
    Guid RiskId,
    NarrativeText Title,
    NarrativeText? Description,
    RiskTreatmentActionType ActionType,
    Guid? OwnerUserId,
    DateOnly? DueDate,
    RiskTreatmentActionStatus Status,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record RiskAcceptanceDetail(
    Guid Id,
    Guid RiskId,
    Guid AcceptedByUserId,
    DateTimeOffset AcceptedAt,
    DateOnly ExpiresOn,
    NarrativeText Rationale,
    RiskAcceptanceStatus Status,
    DateTimeOffset? RevokedAt,
    DateTimeOffset UpdatedAt);
