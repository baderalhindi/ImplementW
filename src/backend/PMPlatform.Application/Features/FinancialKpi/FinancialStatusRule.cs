using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// The financial status of a position (PTBC-025): the forecast's overrun of the Approved Budget, in percent of the budget,
/// against the AMBER and RED thresholds. A position is rated only when its actuals are MEASURED and it has a positive budget and
/// a forecast; anything else is UNKNOWN — a missing, stale or not-applicable figure is never read as zero or as GREEN.
/// </summary>
internal static class FinancialStatusRule
{
    public static FinancialStatus Rate(Money? approvedBudget, Money? forecast, ValueStatus valueStatus, FinancialThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        if (valueStatus != ValueStatus.Measured || approvedBudget is not { Amount: > 0 } budget || forecast is not { } atCompletion)
        {
            return FinancialStatus.Unknown;
        }

        decimal overrunPercent = (atCompletion.Amount - budget.Amount) / budget.Amount * 100m;
        return overrunPercent >= thresholds.RedOverrunPercent ? FinancialStatus.Red
            : overrunPercent >= thresholds.AmberOverrunPercent ? FinancialStatus.Amber
            : FinancialStatus.Green;
    }
}

/// <summary>The overrun, in percent of the Approved Budget, from which a position is AMBER, and from which it is RED; 0 &lt; AMBER ≤ RED.</summary>
internal sealed record FinancialThresholds(decimal AmberOverrunPercent, decimal RedOverrunPercent);
