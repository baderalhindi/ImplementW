namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// The arithmetic of stage-weighted progress (review round 7, item 1), one place for every module that shows it. Overall progress is
/// Σ weight × % complete ÷ Σ weight over the applicable stages, so a stage that does not apply takes its weight out rather than counting as zero.
/// </summary>
public static class StageProgress
{
    /// <summary>A stage's planned % on <paramref name="asOf"/>: 0 before its start, 100 from its end, linear by day between.</summary>
    public static decimal PlannedPercent(DateOnly start, DateOnly end, DateOnly asOf)
    {
        return asOf < start ? 0m
            : asOf >= end ? 100m
            : 100m * (asOf.DayNumber - start.DayNumber) / (end.DayNumber - start.DayNumber);
    }

    /// <summary>Σ w·c ÷ Σ w over <paramref name="stages"/> (the applicable ones); null when they weigh nothing or a percentage is unknown.</summary>
    public static decimal? Overall(IEnumerable<(decimal Weight, decimal? Percent)> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);
        decimal weights = 0m;
        decimal weighted = 0m;
        foreach ((decimal weight, decimal? percent) in stages)
        {
            if (percent is not { } value)
            {
                return null;
            }

            weights += weight;
            weighted += weight * value;
        }

        return weights == 0m ? null : weighted / weights;
    }

    /// <summary>The current stage: the first applicable stage, in stage order, below 100%; the last one when all are complete.</summary>
    public static T? Current<T>(IReadOnlyList<T> applicableInOrder, Func<T, decimal> percentOf)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(applicableInOrder);
        ArgumentNullException.ThrowIfNull(percentOf);
        return applicableInOrder.FirstOrDefault(s => percentOf(s) < 100m) ?? (applicableInOrder.Count == 0 ? null : applicableInOrder[^1]);
    }
}
