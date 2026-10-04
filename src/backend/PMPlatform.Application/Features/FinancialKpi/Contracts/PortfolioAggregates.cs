using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.FinancialKpi.Contracts;

/// <summary>How much of the requested portfolio an aggregate covers (api-conventions R-20(c)).</summary>
public enum AggregateCoverage
{
    Complete = 1,
    Partial = 2,
    None = 3,
}

/// <summary>Why a project's figure is left out of an aggregate.</summary>
public enum AggregateExclusionReason
{
    /// <summary>The project does not exist for the caller.</summary>
    NotAvailable = 1,

    /// <summary>The figure is in a currency that was not verified as SAR (ADR-008: no conversion).</summary>
    CurrencyUnverified = 2,

    /// <summary>The figure is MISSING, STALE or NOT_APPLICABLE, or absent.</summary>
    ValueNotMeasured = 3,

    /// <summary>The caller's audience may not see the figure (ADR-010).</summary>
    Masked = 4,

    /// <summary>The KPI is not assigned to the project, or nothing has been published for it.</summary>
    NoPublishedFigure = 5,
}

public sealed record AggregateExclusion(Guid ProjectId, Guid? KpiDefinitionId, AggregateExclusionReason Reason);

/// <summary>
/// Financial totals over a portfolio of projects, in SAR. A project counts only when its figures are measured, visible to the
/// caller and verified as SAR; otherwise it is listed in <see cref="Exclusions"/> and <see cref="IsPartial"/> is true. With no
/// project counted, every total is null — never 0.
/// </summary>
public sealed record FinancialPortfolioAggregate(
    SemanticState SemanticState,
    string CurrencyCode,
    bool IsPartial,
    AggregateCoverage Coverage,
    int RequestedProjectCount,
    int IncludedProjectCount,
    Money? TotalApprovedBudgetSar,
    Money? TotalActualExpenditureToDateSar,
    Money? TotalForecastAtCompletionSar,
    IReadOnlyList<AggregateExclusion> Exclusions);

/// <summary>The count of each RAG rating among the counted measurements.</summary>
public sealed record KpiRagCounts(int Green, int Amber, int Red, int Unknown, int NotApplicable);

/// <summary>
/// The latest PUBLISHED measurement of each requested KPI on each requested project. Values are combined only when every KPI
/// measures in one unit (<see cref="IsUnitCompatible"/>); otherwise <see cref="MeanValue"/> is null and the aggregate is
/// partial. RAG ratings are unitless and always counted. A measurement without a value counts in the RAG counts and is listed
/// in <see cref="Exclusions"/>.
/// </summary>
public sealed record KpiPortfolioAggregate(
    bool IsUnitCompatible,
    Guid? UnitItemId,
    bool IsPartial,
    AggregateCoverage Coverage,
    int MeasuredCount,
    decimal? MeanValue,
    KpiRagCounts RagCounts,
    IReadOnlyList<AggregateExclusion> Exclusions);
