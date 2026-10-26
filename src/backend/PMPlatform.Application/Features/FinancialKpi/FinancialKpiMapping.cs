using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Domain.FinancialKpi;
using static PMPlatform.Application.Features.FinancialKpi.FinancialKpiMasking;

namespace PMPlatform.Application.Features.FinancialKpi;

internal static class FinancialKpiMapping
{
    public static FinancialSourceModeDetail ToDetail(FinancialSourceMode m) => new(m.Id, m.ProjectId, m.FieldCode, m.SourceMode, m.ConfiguredAt);

    /// <summary>The ACTIVE version is the current one: the database holds at most one per project and type.</summary>
    public static FinancialCommitmentDetail ToDetail(FinancialCommitment c, FieldMask mask) => new(
        c.Id, c.ProjectId, c.CommitmentType, c.VersionNo, c.RevisionNo, c.Status, c.Status == ApprovedVersionStatus.Active, Apply(mask, AmountSar, c.AmountSar),
        c.EffectiveFrom, c.ActivatedAt, c.SupersededByCommitmentId, c.ChangeAuthorizationId, c.ProjectIntakeId, c.SourceType, c.SourceReference, c.AsOfDate,
        c.EnteredByUserId, c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy, mask.MaskedFields);

    public static FinancialProgressUpdateDetail ToDetail(FinancialProgressUpdate u, FieldMask mask) => new(
        u.Id, u.ProjectId, u.ReportingCycleId, u.RevisionNo, u.Status, Apply(mask, ActualExpenditureToDateSar, u.ActualExpenditureToDateSar),
        Apply(mask, ForecastAtCompletionSar, u.ForecastAtCompletionSar), u.ValueStatus, u.Narrative, u.ProjectIntakeId, u.SourceType, u.SourceReference, u.AsOfDate,
        u.EnteredByUserId, u.SubmittedByUserId, u.SubmittedAt, u.ReviewedByUserId, u.ReviewedAt, u.ReturnReason, u.CreatedAt, u.CreatedBy, u.UpdatedAt, u.UpdatedBy,
        mask.MaskedFields);

    public static PublishedFinancialSnapshotDetail ToDetail(PublishedFinancialSnapshot s, FieldMask mask) => new(
        s.Id, s.ProjectId, s.ReportingCycleId, s.FinancialProgressUpdateId, s.FinancialCommitmentId, SemanticState.PublishedOfficial, s.PublishedAt, s.PublishedByUserId,
        Apply(mask, ApprovedBudgetSar, s.ApprovedBudgetSar), Apply(mask, ActualExpenditureToDateSar, s.ActualExpenditureToDateSar),
        Apply(mask, ForecastAtCompletionSar, s.ForecastAtCompletionSar), s.ValueStatus, s.FinancialStatus, s.ThresholdConfigurationVersionId, s.SourceType,
        s.SourceReference, s.AsOfDate, s.EnteredByUserId, mask.MaskedFields);

    public static KpiAssignmentDetail ToDetail(KpiAssignment a, Guid unitItemId) => new(
        a.Id, a.ProjectId, a.KpiDefinitionId, unitItemId, a.OwnerUserId, a.MeasurementFrequencyItemId, a.Status, a.AssignedAt, a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy);

    public static KpiTargetVersionDetail ToDetail(KpiTargetVersion t) => new(
        t.Id, t.KpiAssignmentId, t.VersionNo, t.RevisionNo, t.Status, t.Status == ApprovedVersionStatus.Active, t.TargetValue, t.GreenThreshold, t.AmberThreshold,
        t.EffectiveFrom, t.ActivatedAt, t.SupersededByTargetVersionId, t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy);

    public static KpiMeasurementDetail ToDetail(KpiMeasurement m, Guid projectId, KpiTargetVersion pinned, FieldMask mask) => new(
        m.Id, m.KpiAssignmentId, projectId, m.KpiTargetVersionId, pinned.VersionNo, pinned.TargetValue, m.PeriodStart, m.PeriodEnd, Apply(mask, MeasuredValue, m.MeasuredValue),
        m.ValueStatus, m.RagStatus, m.AsOfDate, m.RecordedByUserId, m.Narrative, m.Status, m.SubmittedAt, m.PublishedByUserId, m.PublishedAt, m.CreatedAt, m.CreatedBy,
        m.UpdatedAt, m.UpdatedBy, mask.MaskedFields);
}
