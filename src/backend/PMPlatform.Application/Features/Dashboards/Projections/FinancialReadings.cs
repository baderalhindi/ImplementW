using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Common.Projections;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// WF-14's financial representations as readings. The figures, their value status, their masking and the Financial Condition are WF-14's
/// (BR-DSH-007); totals are WF-14's own portfolio aggregate, which adds verified SAR figures only and lists every project it left out
/// (DC-DSH-009). An as-of is a WF-14 date and is read as the start of that day, UTC.
/// </summary>
internal static class FinancialReadings
{
    public const string ApprovedBudget = "APPROVED_BUDGET";
    public const string ActualExpenditureToDate = "ACTUAL_EXPENDITURE_TO_DATE";
    public const string ForecastAtCompletion = "FORECAST_AT_COMPLETION";

    // WF-14's representation field names (FinancialKpiMasking), as they appear in its maskedFields.
    private const string ApprovedBudgetSar = "approvedBudgetSar";
    private const string ActualExpenditureToDateSar = "actualExpenditureToDateSar";
    private const string ForecastAtCompletionSar = "forecastAtCompletionSar";

    public static DateTimeOffset? AsOf(DateOnly? date) => date is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;

    public static Observation Of(Guid projectId, Money? budget, Money? actual, Money? forecast, ValueStatus valueStatus, FinancialStatus status, DateTimeOffset? asOf, IReadOnlyList<string> maskedFields)
    {
        ObservationKind kind = valueStatus switch
        {
            ValueStatus.Measured => ObservationKind.Current,
            ValueStatus.Stale => ObservationKind.Stale,
            ValueStatus.NotApplicable => ObservationKind.NotApplicable,
            ValueStatus.Missing => ObservationKind.Missing,
            _ => throw new ArgumentOutOfRangeException(nameof(valueStatus), valueStatus, "Unknown value status."),
        };
        return new Observation(projectId, kind, asOf, ProjectionReadings.State(
            ProjectionReadings.Name(status),
            ProjectionReadings.Sar(ApprovedBudget, budget, maskedFields.Contains(ApprovedBudgetSar)),
            ProjectionReadings.Sar(ActualExpenditureToDate, actual, maskedFields.Contains(ActualExpenditureToDateSar)),
            ProjectionReadings.Sar(ForecastAtCompletion, forecast, maskedFields.Contains(ForecastAtCompletionSar))), maskedFields);
    }

    /// <summary>WF-14's aggregate as a reading: its totals, its own coverage, and its exclusions by reason.</summary>
    public static ProjectionReading Of(FinancialPortfolioAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        List<CoverageExclusion> exclusions =
        [
            .. aggregate.Exclusions.GroupBy(e => e.Reason switch
                {
                    AggregateExclusionReason.Masked or AggregateExclusionReason.NotAvailable => CoverageExclusionReason.Masked,
                    AggregateExclusionReason.CurrencyUnverified => CoverageExclusionReason.Incompatible,
                    AggregateExclusionReason.ValueNotMeasured or AggregateExclusionReason.NoPublishedFigure => CoverageExclusionReason.Missing,
                    _ => throw new ArgumentOutOfRangeException(nameof(aggregate), e.Reason, "Unknown exclusion reason."),
                })
                .OrderBy(g => g.Key)
                .Select(g => new CoverageExclusion(g.Key, g.Select(e => e.ProjectId).Distinct().Count())),
        ];
        int excluded = aggregate.RequestedProjectCount - aggregate.IncludedProjectCount;
        WidgetCoverage coverage = new(aggregate.RequestedProjectCount, aggregate.IncludedProjectCount, excluded, 0, exclusions);
        if (aggregate.Coverage == AggregateCoverage.None)
        {
            bool allMasked = exclusions.Count > 0 && exclusions.All(e => e.Reason == CoverageExclusionReason.Masked);
            return ProjectionReading.Unknown(allMasked ? WidgetUnknownReason.Restricted : WidgetUnknownReason.Missing, coverage);
        }

        return new ProjectionReading(
            ProjectionFreshness.Fresh,
            null,
            AsOf(aggregate.OldestAsOfDate),
            aggregate.Coverage == AggregateCoverage.Complete ? ProjectionCoverage.Complete : ProjectionCoverage.Partial,
            ProjectionReadings.State(
                null,
                ProjectionReadings.Sar(ApprovedBudget, aggregate.TotalApprovedBudgetSar, false),
                ProjectionReadings.Sar(ActualExpenditureToDate, aggregate.TotalActualExpenditureToDateSar, false),
                ProjectionReadings.Sar(ForecastAtCompletion, aggregate.TotalForecastAtCompletionSar, false)),
            coverage,
            []);
    }
}
