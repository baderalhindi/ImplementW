using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// ADR-010 for this module (CTL-19: "sensitive financial fields masked … for external entities"). The entity codes are the ERD
/// entities' names and the field codes the representations' property names, as FIELD_CLASSIFICATION names them. A field the
/// mask does not reveal is null in the application's representation and listed in <c>maskedFields</c>; the API omits every
/// field so listed (R-20). The aggregates leave out a project whose figures the caller's audience may not see.
/// </summary>
internal static class FinancialKpiMasking
{
    public const string Commitment = "FinancialCommitment";
    public const string Update = "FinancialProgressUpdate";
    public const string Snapshot = "PublishedFinancialSnapshot";
    public const string Measurement = "KpiMeasurement";

    public const string AmountSar = "amountSar";
    public const string ApprovedBudgetSar = "approvedBudgetSar";
    public const string ActualExpenditureToDateSar = "actualExpenditureToDateSar";
    public const string ForecastAtCompletionSar = "forecastAtCompletionSar";
    public const string MeasuredValue = "measuredValue";

    public static Money? Apply(FieldMask mask, string fieldCode, Money? value) => mask.Reveals(fieldCode) ? value : null;

    public static decimal? Apply(FieldMask mask, string fieldCode, decimal? value) => mask.Reveals(fieldCode) ? value : null;
}
