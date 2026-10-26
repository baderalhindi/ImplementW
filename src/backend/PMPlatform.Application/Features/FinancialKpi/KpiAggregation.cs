using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <summary>
/// Portfolio aggregation of KPI measurements (TASK-052 acceptance criterion 3). Values are combined only when unit compatibility
/// is verified — every measurement is of a KPI in one and the same KPI_UNIT; otherwise no value is computed and the aggregate is
/// partial. RAG ratings carry no unit and are always counted. A measurement without a value, or one the caller may not see, is
/// listed and makes the aggregate partial.
/// </summary>
internal static class KpiAggregation
{
    public static KpiPortfolioAggregate Aggregate(IReadOnlyList<KpiFigure> figures, IReadOnlyList<AggregateExclusion> unavailable)
    {
        ArgumentNullException.ThrowIfNull(figures);
        ArgumentNullException.ThrowIfNull(unavailable);

        List<Guid> units = [.. figures.Select(f => f.UnitItemId).Distinct()];
        bool compatible = units.Count == 1;
        List<AggregateExclusion> exclusions = [.. unavailable];
        List<decimal> values = [];
        foreach (KpiFigure f in figures)
        {
            if (f.IsMasked)
            {
                exclusions.Add(new AggregateExclusion(f.ProjectId, f.KpiDefinitionId, AggregateExclusionReason.Masked));
            }
            else if (f.ValueStatus != ValueStatus.Measured || f.Value is null)
            {
                exclusions.Add(new AggregateExclusion(f.ProjectId, f.KpiDefinitionId, AggregateExclusionReason.ValueNotMeasured));
            }
            else
            {
                values.Add(f.Value.Value);
            }
        }

        bool computed = compatible && values.Count > 0;
        IReadOnlyList<KpiFigure> rated = [.. figures.Where(f => !f.IsMasked)];
        return new KpiPortfolioAggregate(
            compatible,
            compatible ? units[0] : null,
            exclusions.Count > 0 || !compatible,
            !computed ? AggregateCoverage.None : exclusions.Count > 0 ? AggregateCoverage.Partial : AggregateCoverage.Complete,
            values.Count,
            computed ? values.Average() : null,
            new KpiRagCounts(
                rated.Count(f => f.RagStatus == KpiRagStatus.Green),
                rated.Count(f => f.RagStatus == KpiRagStatus.Amber),
                rated.Count(f => f.RagStatus == KpiRagStatus.Red),
                rated.Count(f => f.RagStatus == KpiRagStatus.Unknown),
                rated.Count(f => f.RagStatus == KpiRagStatus.NotApplicable)),
            [.. exclusions.OrderBy(e => e.ProjectId).ThenBy(e => e.KpiDefinitionId)]);
    }
}

/// <summary>One project's latest published measurement of one KPI, with the unit its definition measures in.</summary>
internal sealed record KpiFigure(Guid ProjectId, Guid KpiDefinitionId, Guid UnitItemId, decimal? Value, ValueStatus ValueStatus, KpiRagStatus RagStatus, bool IsMasked);
