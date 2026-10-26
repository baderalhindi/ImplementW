using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// A measurement's RAG against its pinned target version (OQ-006), by the KPI's direction. The thresholds are values in the KPI's
/// unit: for HIGHER_IS_BETTER a value at or above GREEN is GREEN and at or above AMBER is AMBER; for LOWER_IS_BETTER at or below;
/// for TARGET_BAND they are the largest distance from the target that is still GREEN, and AMBER. NOT_APPLICABLE stays
/// NOT_APPLICABLE; no value, or no thresholds, is UNKNOWN. Nothing is coerced to a colour.
/// </summary>
internal static class KpiRagRule
{
    public static KpiRagStatus Rate(decimal? value, ValueStatus valueStatus, KpiDirection direction, decimal target, decimal? green, decimal? amber)
    {
        return valueStatus == ValueStatus.NotApplicable ? KpiRagStatus.NotApplicable
            : valueStatus != ValueStatus.Measured || value is not { } measured || green is not { } g || amber is not { } a
            ? KpiRagStatus.Unknown
            : direction switch
            {
                KpiDirection.HigherIsBetter => measured >= g ? KpiRagStatus.Green : measured >= a ? KpiRagStatus.Amber : KpiRagStatus.Red,
                KpiDirection.LowerIsBetter => measured <= g ? KpiRagStatus.Green : measured <= a ? KpiRagStatus.Amber : KpiRagStatus.Red,
                KpiDirection.TargetBand => Math.Abs(measured - target) is var distance && distance <= g ? KpiRagStatus.Green
                    : distance <= a ? KpiRagStatus.Amber
                    : KpiRagStatus.Red,
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown KPI direction."),
            };
    }

    /// <summary>
    /// Whether a target's thresholds can rate anything by the direction: both or neither (neither leaves every rating UNKNOWN),
    /// and ordered — GREEN not below AMBER when higher is better, not above it when lower is better, and 0 ≤ GREEN ≤ AMBER as
    /// distances for a band.
    /// </summary>
    public static bool AreCoherent(KpiDirection direction, decimal? green, decimal? amber) => (green, amber) switch
    {
        (null, null) => true,
        ({ } g, { } a) => direction switch
        {
            KpiDirection.HigherIsBetter => g >= a,
            KpiDirection.LowerIsBetter => g <= a,
            KpiDirection.TargetBand => g >= 0 && g <= a,
            _ => false,
        },
        _ => false,
    };
}
