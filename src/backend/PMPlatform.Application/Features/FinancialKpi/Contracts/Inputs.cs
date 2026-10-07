using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>A field's source mode, set for the first time.</summary>
public sealed record FinancialSourceModeDraft(Guid ProjectId, FinancialField FieldCode, SourceMode SourceMode);

/// <summary>
/// A new Approved Budget version: the project total in SAR, when it applies from, and its manual provenance (ADR-008): the
/// document or system reference and the day the figure is true as of. Who entered it is the caller.
/// </summary>
public sealed record FinancialCommitmentDraft(Guid ProjectId, Money AmountSar, DateOnly? EffectiveFrom, string? SourceReference, DateOnly AsOfDate);

/// <summary>A DRAFT or RETURNED version's figures, as a whole.</summary>
public sealed record FinancialCommitmentChanges(Money AmountSar, DateOnly? EffectiveFrom, string? SourceReference, DateOnly AsOfDate);

/// <summary>Submitting a version; a change to an ACTIVE Approved Budget names the approved WF-08 change authorisation it implements (TASK-060).</summary>
public sealed record CommitmentSubmission(Guid? ChangeAuthorizationId);

/// <summary>A document to attach to a commitment version as its reference, the version of it to pin, and its EVIDENCE_TYPE.</summary>
public sealed record CommitmentDocumentAttachment(Guid DocumentId, Guid DocumentVersionId, Guid EvidenceTypeItemId);

/// <summary>
/// A DRAFT update's figures, as a whole. A figure that is not known is sent as null with the value status saying why; it is
/// never sent as 0.
/// </summary>
public sealed record FinancialProgressUpdateChanges(
    Money? ActualExpenditureToDateSar, Money? ForecastAtCompletionSar, ValueStatus ValueStatus, NarrativeText? Narrative, string? SourceReference, DateOnly AsOfDate);

public sealed record KpiAssignmentDraft(Guid ProjectId, Guid KpiDefinitionId, Guid? OwnerUserId, Guid MeasurementFrequencyItemId);

public sealed record KpiAssignmentChanges(Guid? OwnerUserId, Guid MeasurementFrequencyItemId);

public sealed record KpiTargetVersionDraft(Guid KpiAssignmentId, decimal TargetValue, decimal? GreenThreshold, decimal? AmberThreshold, DateOnly? EffectiveFrom);

public sealed record KpiTargetVersionChanges(decimal TargetValue, decimal? GreenThreshold, decimal? AmberThreshold, DateOnly? EffectiveFrom);

public sealed record KpiMeasurementDraft(
    Guid KpiAssignmentId, DateOnly PeriodStart, DateOnly PeriodEnd, decimal? MeasuredValue, ValueStatus ValueStatus, DateOnly AsOfDate, NarrativeText? Narrative);

/// <summary>A DRAFT measurement's figure, as a whole. Its period and its pinned target version never change.</summary>
public sealed record KpiMeasurementChanges(decimal? MeasuredValue, ValueStatus ValueStatus, DateOnly AsOfDate, NarrativeText? Narrative);
