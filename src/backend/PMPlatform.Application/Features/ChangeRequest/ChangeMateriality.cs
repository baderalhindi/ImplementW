using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// The materiality rule of ADR-016 (TASK-106's shape; values OQ-013): three bands per governance profile, each with thresholds on
/// cost, schedule and scope; a change takes the highest band any dimension triggers, and changes accumulate against the active
/// baseline. Generic: no threshold, band count beyond three or rule code is named in code.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>A dimension applies only when the request states an impact on it; otherwise its band is null, not 1.</item>
/// <item>Cost and schedule are judged on the absolute cumulative position — a reduction is a change too. A band's absolute and
/// percentage thresholds are independent alternatives: either reached triggers it. A percentage is of the active baseline's budget
/// or duration.</item>
/// <item>Scope escalates by rule: a request with a scope impact takes the highest band that names a scope rule.</item>
/// <item>A dimension that reaches no band's threshold is band 1, the lowest; a contractual obligation is band 3 (TASK-106).</item>
/// </list>
/// </remarks>
internal static class ChangeMateriality
{
    public const short LowestBand = 1;
    public const short HighestBand = 3;

    public static MaterialityBands Classify(IReadOnlyList<MaterialityBandEntry> bands, MaterialityInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(bands);
        ArgumentNullException.ThrowIfNull(inputs);
        IReadOnlyList<MaterialityBandEntry> highestFirst = [.. bands.OrderByDescending(b => b.BandNo)];

        short? cost = !inputs.HasCostImpact ? null
            : BandOf(highestFirst, b => Reaches(Math.Abs(inputs.CumulativeCost.Amount), b.CostThresholdSar, b.CostThresholdPct, BaseOf(inputs.BudgetBase?.Amount, "budget")));
        short? schedule = !inputs.HasScheduleImpact ? null
            : BandOf(highestFirst, b => Reaches(Math.Abs(inputs.CumulativeScheduleDays), b.ScheduleThresholdDays, b.ScheduleThresholdPct, BaseOf(inputs.DurationBase, "duration")));
        short? scope = !inputs.HasScopeImpact ? null : BandOf(highestFirst, b => b.ScopeRuleCode is not null);

        short resulting = new short?[] { cost, schedule, scope, inputs.IsContractualObligation ? HighestBand : null, LowestBand }.Max()!.Value;
        return new MaterialityBands(cost, schedule, scope, resulting);
    }

    /// <summary>The highest band <paramref name="triggers"/>, else the lowest.</summary>
    private static short BandOf(IReadOnlyList<MaterialityBandEntry> highestFirst, Func<MaterialityBandEntry, bool> triggers) =>
        highestFirst.FirstOrDefault(triggers)?.BandNo ?? LowestBand;

    /// <summary>Whether <paramref name="value"/> reaches the absolute threshold, or the percentage of <paramref name="baseValue"/>.</summary>
    private static bool Reaches(decimal value, decimal? absolute, decimal? percent, decimal baseValue) =>
        (absolute is { } a && value >= a) || (percent is { } p && value * 100m >= p * baseValue);

    /// <summary>Missing is never read as zero: a dimension that applies has its base.</summary>
    private static decimal BaseOf(decimal? value, string name) =>
        value ?? throw new InvalidOperationException($"A materiality dimension applies without its {name} base.");
}

/// <summary>
/// What the rule judges: each dimension's cumulative position, whether the request states an impact on it, and the active baseline's
/// figure a percentage threshold is of — present whenever its dimension applies.
/// </summary>
internal sealed record MaterialityInputs(
    Money CumulativeCost,
    bool HasCostImpact,
    Money? BudgetBase,
    int CumulativeScheduleDays,
    bool HasScheduleImpact,
    int? DurationBase,
    bool HasScopeImpact,
    bool IsContractualObligation);

/// <summary>The band each dimension triggers — null where none applies — and the resulting band.</summary>
internal sealed record MaterialityBands(short? Cost, short? Schedule, short? Scope, short Resulting);
