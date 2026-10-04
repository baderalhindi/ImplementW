using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Api.Models.FinancialKpi;

/// <summary>Assigns a PUBLISHED catalogue KPI to a project: its owner, if any, and a PUBLISHED MEASUREMENT_FREQUENCY item.</summary>
public sealed record KpiAssignmentCreateRequest(Guid? ProjectId, Guid? KpiDefinitionId, Guid? OwnerUserId, Guid? MeasurementFrequencyItemId)
{
    internal List<FieldError> Validate(out KpiAssignmentDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ProjectId, "projectId", errors);
        RequestValidation.RequireId(KpiDefinitionId, "kpiDefinitionId", errors);
        RequestValidation.RequireId(MeasurementFrequencyItemId, "measurementFrequencyItemId", errors);
        draft = errors.Count == 0 ? new KpiAssignmentDraft(ProjectId!.Value, KpiDefinitionId!.Value, OwnerUserId, MeasurementFrequencyItemId!.Value) : null;
        return errors;
    }
}

/// <summary>An assignment's owner and frequency, as a whole (R-5). Its project and KPI never change.</summary>
public sealed record KpiAssignmentRequest(Guid? OwnerUserId, Guid? MeasurementFrequencyItemId)
{
    internal List<FieldError> Validate(out KpiAssignmentChanges? changes)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(MeasurementFrequencyItemId, "measurementFrequencyItemId", errors);
        changes = errors.Count == 0 ? new KpiAssignmentChanges(OwnerUserId, MeasurementFrequencyItemId!.Value) : null;
        return errors;
    }
}

/// <summary>
/// A target version's figures, as a whole (R-5): the target in the KPI's unit and the RAG thresholds, both or neither, to four
/// places (numeric(18,4)).
/// </summary>
public sealed record KpiTargetVersionRequest(decimal? TargetValue, decimal? GreenThreshold, decimal? AmberThreshold, DateOnly? EffectiveFrom)
{
    internal List<FieldError> Validate(out KpiTargetVersionChanges? changes)
    {
        List<FieldError> errors = [];
        FinancialKpiFields.Require(TargetValue, "targetValue", errors);
        KpiValue.Places(TargetValue, "targetValue", errors);
        KpiValue.Places(GreenThreshold, "greenThreshold", errors);
        KpiValue.Places(AmberThreshold, "amberThreshold", errors);
        changes = errors.Count == 0 ? new KpiTargetVersionChanges(TargetValue!.Value, GreenThreshold, AmberThreshold, EffectiveFrom) : null;
        return errors;
    }
}

/// <summary>A new target version of the assignment: <see cref="KpiTargetVersionRequest"/> and the assignment.</summary>
public sealed record KpiTargetVersionCreateRequest(Guid? KpiAssignmentId, decimal? TargetValue, decimal? GreenThreshold, decimal? AmberThreshold, DateOnly? EffectiveFrom)
{
    internal List<FieldError> Validate(out KpiTargetVersionDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(KpiAssignmentId, "kpiAssignmentId", errors);
        errors.AddRange(new KpiTargetVersionRequest(TargetValue, GreenThreshold, AmberThreshold, EffectiveFrom).Validate(out _));
        draft = errors.Count == 0 ? new KpiTargetVersionDraft(KpiAssignmentId!.Value, TargetValue!.Value, GreenThreshold, AmberThreshold, EffectiveFrom) : null;
        return errors;
    }
}

/// <summary>
/// A DRAFT measurement's value, as a whole (R-5). A value that is not known is sent as null with <c>valueStatus</c> saying why —
/// never as 0. Its period and its pinned target version never change.
/// </summary>
public sealed record KpiMeasurementRequest(decimal? MeasuredValue, ValueStatus? ValueStatus, DateOnly? AsOfDate, NarrativeTextRequest? Narrative)
{
    internal List<FieldError> Validate(out KpiMeasurementChanges? changes)
    {
        List<FieldError> errors = [];
        KpiValue.Places(MeasuredValue, "measuredValue", errors);
        FinancialKpiFields.Require(ValueStatus, "valueStatus", errors);
        FinancialKpiFields.Require(AsOfDate, "asOfDate", errors);
        NarrativeText? narrative = Narrative?.Validate("narrative", errors);
        changes = errors.Count == 0 ? new KpiMeasurementChanges(MeasuredValue, ValueStatus!.Value, AsOfDate!.Value, narrative) : null;
        return errors;
    }
}

/// <summary>A new measurement of the assignment for a period: <see cref="KpiMeasurementRequest"/>, the assignment and the period.</summary>
public sealed record KpiMeasurementCreateRequest(
    Guid? KpiAssignmentId, DateOnly? PeriodStart, DateOnly? PeriodEnd, decimal? MeasuredValue, ValueStatus? ValueStatus, DateOnly? AsOfDate, NarrativeTextRequest? Narrative)
{
    internal List<FieldError> Validate(out KpiMeasurementDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(KpiAssignmentId, "kpiAssignmentId", errors);
        FinancialKpiFields.Require(PeriodStart, "periodStart", errors);
        FinancialKpiFields.Require(PeriodEnd, "periodEnd", errors);
        errors.AddRange(new KpiMeasurementRequest(MeasuredValue, ValueStatus, AsOfDate, Narrative).Validate(out KpiMeasurementChanges? changes));
        draft = errors.Count == 0
            ? new KpiMeasurementDraft(KpiAssignmentId!.Value, PeriodStart!.Value, PeriodEnd!.Value, changes!.MeasuredValue, changes.ValueStatus, changes.AsOfDate, changes.Narrative)
            : null;
        return errors;
    }
}

/// <summary>A KPI figure is a numeric(18,4): four places at most.</summary>
internal static class KpiValue
{
    public static void Places(decimal? value, string field, List<FieldError> errors)
    {
        if (value is { } v && (decimal.Round(v, 4) != v || Math.Abs(v) >= 100_000_000_000_000m))
        {
            errors.Add(new FieldError(field, FieldError.OutOfRange));
        }
    }
}
