using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// Portfolio totals (TASK-052 acceptance criterion 3). A project's figures are added only when their currency is verified as
/// SAR — ADR-008 has no conversion and no rate source, so a figure in any other currency, or of unknown currency, is never
/// added — and they are MEASURED, complete and visible to the caller. Every project left out is listed with its reason and
/// makes the aggregate partial. With none counted, the totals are null, never 0.
/// </summary>
internal static class FinancialAggregation
{
    public static FinancialPortfolioAggregate Aggregate(SemanticState semanticState, int requestedProjectCount, IReadOnlyList<FinancialFigures> figures, IReadOnlyList<AggregateExclusion> unavailable)
    {
        ArgumentNullException.ThrowIfNull(figures);
        ArgumentNullException.ThrowIfNull(unavailable);

        List<AggregateExclusion> exclusions = [.. unavailable];
        List<FinancialFigures> counted = [];
        foreach (FinancialFigures f in figures)
        {
            AggregateExclusionReason? reason =
                f.IsMasked ? AggregateExclusionReason.Masked
                : !string.Equals(f.CurrencyCode, Money.CurrencyCode, StringComparison.Ordinal) ? AggregateExclusionReason.CurrencyUnverified
                : f.ValueStatus != ValueStatus.Measured || f.ApprovedBudget is null || f.ActualExpenditure is null || f.Forecast is null ? AggregateExclusionReason.ValueNotMeasured
                : null;
            if (reason is { } excluded)
            {
                exclusions.Add(new AggregateExclusion(f.ProjectId, null, excluded));
            }
            else
            {
                counted.Add(f);
            }
        }

        bool any = counted.Count > 0;
        return new FinancialPortfolioAggregate(
            semanticState,
            Money.CurrencyCode,
            exclusions.Count > 0,
            !any ? AggregateCoverage.None : exclusions.Count > 0 ? AggregateCoverage.Partial : AggregateCoverage.Complete,
            requestedProjectCount,
            counted.Count,
            any ? Sum(counted.Select(f => f.ApprovedBudget!.Value)) : null,
            any ? Sum(counted.Select(f => f.ActualExpenditure!.Value)) : null,
            any ? Sum(counted.Select(f => f.Forecast!.Value)) : null,
            [.. exclusions.OrderBy(e => e.ProjectId)]);
    }

    private static Money Sum(IEnumerable<Money> amounts) => amounts.Aggregate(Money.Zero, (total, amount) => total + amount);
}

/// <summary>
/// One project's figures as a source states them, with the currency it states them in. Every figure the platform holds is a
/// <see cref="Money"/>, whose currency is SAR by construction; an integrated source states its own (financial-kpi.md D-11).
/// </summary>
internal sealed record FinancialFigures(
    Guid ProjectId, string CurrencyCode, Money? ApprovedBudget, Money? ActualExpenditure, Money? Forecast, ValueStatus ValueStatus, bool IsMasked);
