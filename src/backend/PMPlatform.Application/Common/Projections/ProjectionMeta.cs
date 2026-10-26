namespace PMPlatform.Application.Common.Projections;

/// <summary>
/// The projection metadata contract (api-conventions R-20(c), TASK-069): what every value a dashboard or report presents says
/// about itself. It is never merged with another semantic state, a freshness it cannot establish is <see cref="ProjectionFreshness.Unknown"/>
/// rather than assumed, and an absent value stays absent: "explicitly Unknown/Stale in the payload, never coerced to zero or a
/// default". Defined once, here (M-10), for FG-01 and FG-02 alike.
/// </summary>
/// <param name="SemanticState">Which of the source's views the value is: live, published, or a historical snapshot.</param>
/// <param name="Freshness">Whether the source says the value is current, out of date, or cannot say.</param>
/// <param name="AsOf">When the source data represented was current; null when there is no value to date.</param>
/// <param name="Coverage">How much of the eligible population the value covers.</param>
public sealed record ProjectionMeta(ProjectionSemanticState SemanticState, ProjectionFreshness Freshness, DateTimeOffset? AsOf, ProjectionCoverage Coverage);

/// <summary>R-20(c) <c>semanticState</c>: the three views FG-01 §7.1 keeps apart (CURRENT_OPERATIONAL, CURRENT_PUBLISHED, HISTORICAL_SNAPSHOT).</summary>
public enum ProjectionSemanticState
{
    /// <summary>The latest operational state, which may still change.</summary>
    CurrentLive = 1,

    /// <summary>The latest controlled, published snapshot.</summary>
    PublishedOfficial = 2,

    /// <summary>An immutable prior snapshot or period, as the source keeps it.</summary>
    HistoricalSnapshot = 3,
}

/// <summary>R-20(c) <c>freshness</c>. UNKNOWN is a value of its own: a missing, restricted or unavailable source is never FRESH and never 0.</summary>
public enum ProjectionFreshness
{
    Fresh = 1,
    Stale = 2,
    Unknown = 3,
}

/// <summary>R-20(c) <c>coverage</c>: all of the eligible population, some of it, or none of it.</summary>
public enum ProjectionCoverage
{
    Complete = 1,
    Partial = 2,
    None = 3,
}
