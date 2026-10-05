using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>
/// The source mode of one financial field of a project. A field never configured is MANUAL, the launch mode, with no
/// <see cref="Id"/> (ADR-008).
/// </summary>
public sealed record FinancialSourceModeDetail(Guid? Id, Guid ProjectId, FinancialField FieldCode, SourceMode SourceMode, DateTimeOffset? ConfiguredAt);

/// <summary>
/// One commitment version. <see cref="IsCurrent"/> marks the ACTIVE one, the budget of record. A field the caller's audience may
/// not see (ADR-010) is null here and named in <see cref="MaskedFields"/>; the API omits it (R-20). Its review history is
/// <c>GET /approval-instances?subjectModule=FinancialKpi&amp;subjectType=FinancialCommitment&amp;subjectId={id}</c>.
/// </summary>
public sealed record FinancialCommitmentDetail(
    Guid Id,
    Guid ProjectId,
    CommitmentType CommitmentType,
    int VersionNo,
    int RevisionNo,
    ApprovedVersionStatus Status,
    bool IsCurrent,
    Money? AmountSar,
    DateOnly? EffectiveFrom,
    DateTimeOffset? ActivatedAt,
    Guid? SupersededByCommitmentId,
    Guid? ChangeAuthorizationId,
    Guid? ProjectIntakeId,
    FinancialSourceType SourceType,
    string? SourceReference,
    DateOnly AsOfDate,
    Guid EnteredByUserId,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    IReadOnlyList<string> MaskedFields);

/// <summary>
/// One revision of a period's CURRENT/LIVE actuals and forecast. A figure that is not known is null and
/// <see cref="ValueStatus"/> says why — never 0 (TASK-052). Masked fields as <see cref="FinancialCommitmentDetail"/>.
/// </summary>
public sealed record FinancialProgressUpdateDetail(
    Guid Id,
    Guid ProjectId,
    Guid ReportingCycleId,
    int RevisionNo,
    FinancialUpdateStatus Status,
    Money? ActualExpenditureToDateSar,
    Money? ForecastAtCompletionSar,
    ValueStatus ValueStatus,
    NarrativeText? Narrative,
    Guid? ProjectIntakeId,
    FinancialSourceType SourceType,
    string? SourceReference,
    DateOnly AsOfDate,
    Guid EnteredByUserId,
    Guid? SubmittedByUserId,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    NarrativeText? ReturnReason,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    IReadOnlyList<string> MaskedFields);

/// <summary>The PUBLISHED/OFFICIAL financial position of a period, as stored at publication and never since changed.</summary>
public sealed record PublishedFinancialSnapshotDetail(
    Guid Id,
    Guid ProjectId,
    Guid ReportingCycleId,
    Guid FinancialProgressUpdateId,
    Guid? FinancialCommitmentId,
    SemanticState SemanticState,
    DateTimeOffset PublishedAt,
    Guid PublishedByUserId,
    Money? ApprovedBudgetSar,
    Money? ActualExpenditureToDateSar,
    Money? ForecastAtCompletionSar,
    ValueStatus ValueStatus,
    FinancialStatus FinancialStatus,
    Guid ThresholdConfigurationVersionId,
    FinancialSourceType SourceType,
    string? SourceReference,
    DateOnly AsOfDate,
    Guid EnteredByUserId,
    IReadOnlyList<string> MaskedFields)
{
    /// <summary>TASK-054 validation only, never to merge: a second Overall Health, computed by WF-14 from its own status.</summary>
    public FinancialStatus OverallHealth => FinancialStatus is FinancialStatus.Unknown ? FinancialStatus.Unknown : FinancialStatus;
}

/// <summary>
/// A project's CURRENT/LIVE financial position, computed on read and never stored: the ACTIVE Approved Budget, and the figures of
/// the latest revision submitted for review or published. <see cref="FinancialStatus"/> is rated under the thresholds in force
/// now, UNKNOWN when there are none or a figure is missing (<see cref="ThresholdConfigurationVersionId"/> null). Distinct from
/// every <see cref="PublishedFinancialSnapshotDetail"/> (M-12).
/// </summary>
public sealed record FinancialPositionDetail(
    Guid ProjectId,
    SemanticState SemanticState,
    Guid? FinancialCommitmentId,
    Money? ApprovedBudgetSar,
    Guid? FinancialProgressUpdateId,
    FinancialUpdateStatus? UpdateStatus,
    Money? ActualExpenditureToDateSar,
    Money? ForecastAtCompletionSar,
    ValueStatus ValueStatus,
    FinancialStatus FinancialStatus,
    Guid? ThresholdConfigurationVersionId,
    DateOnly? AsOfDate,
    DateTimeOffset ComputedAt,
    IReadOnlyList<string> MaskedFields);

/// <summary>
/// A commitment version's referenced documents (ADR-008 gate): its document links, and the evidence types it holds satisfying
/// evidence for (a VALID reference to a CLEAN version on an active link). Submission needs at least one.
/// </summary>
public sealed record CommitmentDocumentDetail(Guid FinancialCommitmentId, IReadOnlyList<Guid> SatisfiedEvidenceTypeItemIds, IReadOnlyList<BusinessLinkDetail> Links);
