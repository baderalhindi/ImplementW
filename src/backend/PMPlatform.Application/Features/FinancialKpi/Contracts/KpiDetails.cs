using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>A KPI assigned to a project, with the definition's unit, so a reader can tell which measurements compare.</summary>
public sealed record KpiAssignmentDetail(
    Guid Id,
    Guid ProjectId,
    Guid KpiDefinitionId,
    Guid UnitItemId,
    Guid? OwnerUserId,
    Guid MeasurementFrequencyItemId,
    KpiAssignmentStatus Status,
    DateTimeOffset AssignedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>
/// One target version. <see cref="IsCurrent"/> marks the ACTIVE one, the version a measurement recorded now pins. Its review
/// history is <c>GET /approval-instances?subjectModule=FinancialKpi&amp;subjectType=KpiTargetVersion&amp;subjectId={id}</c>.
/// </summary>
public sealed record KpiTargetVersionDetail(
    Guid Id,
    Guid KpiAssignmentId,
    int VersionNo,
    int RevisionNo,
    ApprovedVersionStatus Status,
    bool IsCurrent,
    decimal TargetValue,
    decimal? GreenThreshold,
    decimal? AmberThreshold,
    DateOnly? EffectiveFrom,
    DateTimeOffset? ActivatedAt,
    Guid? SupersededByTargetVersionId,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>
/// A measurement and the target version it is pinned to, as that version stands (it never changes once ACTIVE). A value that is
/// not known is null and <see cref="ValueStatus"/> says why; <see cref="RagStatus"/> is then UNKNOWN or NOT_APPLICABLE — never 0,
/// never GREEN. Masked fields as <see cref="FinancialCommitmentDetail"/>.
/// </summary>
public sealed record KpiMeasurementDetail(
    Guid Id,
    Guid KpiAssignmentId,
    Guid ProjectId,
    Guid KpiTargetVersionId,
    int TargetVersionNo,
    decimal TargetValue,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal? MeasuredValue,
    ValueStatus ValueStatus,
    KpiRagStatus RagStatus,
    DateOnly AsOfDate,
    Guid RecordedByUserId,
    NarrativeText? Narrative,
    KpiMeasurementStatus Status,
    DateTimeOffset? SubmittedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    IReadOnlyList<string> MaskedFields);
