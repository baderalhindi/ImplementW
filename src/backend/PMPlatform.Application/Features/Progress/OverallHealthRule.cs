using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// ICD-03: the Overall Project Health calculation, which exists here and nowhere else (M-12; an architecture test holds
/// every other module to it). Three dimensions: progress — the slippage of actual behind planned, in percentage points,
/// against the configured AMBER and RED thresholds — and WF-03's Schedule Health and WF-14's financial status as those
/// modules publish them. Overall is the worst of the three. A dimension without its inputs is UNKNOWN, and so is the
/// overall: a missing input is never coerced to a colour. The thresholds are AHDA's (progress-update.md F-3).
/// </summary>
internal static class OverallHealthRule
{
    public static HealthStatus Compute(
        decimal? actualPercent, decimal? plannedPercent, HealthStatus? scheduleHealth, HealthStatus? financialStatus, HealthThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        HealthStatus[] dimensions = [Progress(actualPercent, plannedPercent, thresholds), scheduleHealth ?? HealthStatus.Unknown, financialStatus ?? HealthStatus.Unknown];
        return dimensions.Contains(HealthStatus.Unknown) ? HealthStatus.Unknown
            : dimensions.Contains(HealthStatus.Red) ? HealthStatus.Red
            : dimensions.Contains(HealthStatus.Amber) ? HealthStatus.Amber
            : HealthStatus.Green;
    }

    /// <summary>GREEN below the AMBER slippage, AMBER from it, RED from the RED slippage. Ahead of plan is GREEN.</summary>
    public static HealthStatus Progress(decimal? actualPercent, decimal? plannedPercent, HealthThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        if (actualPercent is not { } actual || plannedPercent is not { } planned)
        {
            return HealthStatus.Unknown;
        }

        decimal slippage = planned - actual;
        return slippage >= thresholds.RedSlippagePercent ? HealthStatus.Red
            : slippage >= thresholds.AmberSlippagePercent ? HealthStatus.Amber
            : HealthStatus.Green;
    }
}

/// <summary>Slippage of actual behind planned, in percentage points, from which progress is AMBER and RED; 0 &lt; AMBER ≤ RED.</summary>
internal sealed record HealthThresholds(decimal AmberSlippagePercent, decimal RedSlippagePercent);
