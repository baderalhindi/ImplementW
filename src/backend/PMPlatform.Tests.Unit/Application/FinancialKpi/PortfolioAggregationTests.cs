using PMPlatform.Application.Features.FinancialKpi;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Tests.Unit.Application.FinancialKpi;

/// <summary>
/// TASK-052 acceptance criterion 3: a portfolio aggregate is computed only over figures whose currency or unit compatibility is
/// verified; anything else is left out, listed, and the aggregate is marked partial. Nothing missing is added as zero.
/// </summary>
public sealed class PortfolioAggregationTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-4000-8000-0000000000a1");
    private static readonly Guid B = Guid.Parse("00000000-0000-4000-8000-0000000000a2");
    private static readonly Guid C = Guid.Parse("00000000-0000-4000-8000-0000000000a3");
    private static readonly Guid Percent = Guid.Parse("00000000-0000-4000-8000-0000000000b1");
    private static readonly Guid Days = Guid.Parse("00000000-0000-4000-8000-0000000000b2");
    private static readonly Guid Kpi1 = Guid.Parse("00000000-0000-4000-8000-0000000000c1");
    private static readonly Guid Kpi2 = Guid.Parse("00000000-0000-4000-8000-0000000000c2");

    [Fact]
    public void VerifiedSarFiguresAreAddedAndTheAggregateIsComplete()
    {
        FinancialPortfolioAggregate aggregate = FinancialAggregation.Aggregate(SemanticState.PublishedOfficial, 2, [Measured(A, 100, 40, 110), Measured(B, 200, 50, 190)], []);

        Assert.Equal((false, AggregateCoverage.Complete, 2), (aggregate.IsPartial, aggregate.Coverage, aggregate.IncludedProjectCount));
        Assert.Equal((new Money(300m), new Money(90m), new Money(300m)), (aggregate.TotalApprovedBudgetSar, aggregate.TotalActualExpenditureToDateSar, aggregate.TotalForecastAtCompletionSar));
        Assert.Equal("SAR", aggregate.CurrencyCode);
    }

    /// <summary>ADR-008: no conversion and no rate source, so a figure in a currency not verified as SAR is never added.</summary>
    [Fact]
    public void AFigureWhoseCurrencyIsNotVerifiedIsLeftOutAndTheAggregateIsPartial()
    {
        FinancialPortfolioAggregate aggregate = FinancialAggregation.Aggregate(
            SemanticState.PublishedOfficial, 2, [Measured(A, 100, 40, 110), Measured(B, 200, 50, 190) with { CurrencyCode = "USD" }], []);

        Assert.Equal((true, AggregateCoverage.Partial, 1), (aggregate.IsPartial, aggregate.Coverage, aggregate.IncludedProjectCount));
        Assert.Equal(new Money(100m), aggregate.TotalApprovedBudgetSar);
        Assert.Equal([new AggregateExclusion(B, null, AggregateExclusionReason.CurrencyUnverified)], aggregate.Exclusions);
    }

    [Fact]
    public void AMissingFigureIsLeftOutNotAddedAsZero()
    {
        FinancialFigures missing = new(B, Money.CurrencyCode, new Money(200m), null, null, ValueStatus.Missing, false);
        FinancialPortfolioAggregate aggregate = FinancialAggregation.Aggregate(SemanticState.CurrentLive, 3, [Measured(A, 100, 40, 110), missing],
            [new AggregateExclusion(C, null, AggregateExclusionReason.NotAvailable)]);

        Assert.True(aggregate.IsPartial);
        Assert.Equal(new Money(100m), aggregate.TotalApprovedBudgetSar);
        Assert.Equal(
            [new AggregateExclusion(B, null, AggregateExclusionReason.ValueNotMeasured), new AggregateExclusion(C, null, AggregateExclusionReason.NotAvailable)],
            aggregate.Exclusions);
    }

    [Fact]
    public void WithNothingCountedEveryTotalIsNullNeverZero()
    {
        FinancialPortfolioAggregate aggregate = FinancialAggregation.Aggregate(
            SemanticState.PublishedOfficial, 1, [new FinancialFigures(A, Money.CurrencyCode, null, null, null, ValueStatus.NotApplicable, false)], []);

        Assert.Equal(AggregateCoverage.None, aggregate.Coverage);
        Assert.Null(aggregate.TotalApprovedBudgetSar);
        Assert.Null(aggregate.TotalActualExpenditureToDateSar);
        Assert.Null(aggregate.TotalForecastAtCompletionSar);
    }

    [Fact]
    public void AMaskedFigureIsLeftOut()
    {
        FinancialPortfolioAggregate aggregate = FinancialAggregation.Aggregate(SemanticState.PublishedOfficial, 1, [Measured(A, 100, 40, 110) with { IsMasked = true }], []);

        Assert.Equal([new AggregateExclusion(A, null, AggregateExclusionReason.Masked)], aggregate.Exclusions);
        Assert.Null(aggregate.TotalApprovedBudgetSar);
    }

    [Fact]
    public void KpiValuesInOneUnitAreCombined()
    {
        KpiPortfolioAggregate aggregate = KpiAggregation.Aggregate([Kpi(A, Kpi1, Percent, 90m, KpiRagStatus.Amber), Kpi(B, Kpi1, Percent, 96m, KpiRagStatus.Green)], []);

        Assert.Equal((true, Percent, false, AggregateCoverage.Complete, 2, 93m), (aggregate.IsUnitCompatible, aggregate.UnitItemId, aggregate.IsPartial, aggregate.Coverage, aggregate.MeasuredCount, aggregate.MeanValue));
        Assert.Equal(new KpiRagCounts(1, 1, 0, 0, 0), aggregate.RagCounts);
    }

    /// <summary>Two units cannot be combined: no value is computed and the aggregate is partial; the unitless RAG is still counted.</summary>
    [Fact]
    public void KpiValuesInDifferentUnitsAreNotCombinedAndTheAggregateIsPartial()
    {
        KpiPortfolioAggregate aggregate = KpiAggregation.Aggregate([Kpi(A, Kpi1, Percent, 90m, KpiRagStatus.Amber), Kpi(A, Kpi2, Days, 12m, KpiRagStatus.Red)], []);

        Assert.Equal((false, true, AggregateCoverage.None), (aggregate.IsUnitCompatible, aggregate.IsPartial, aggregate.Coverage));
        Assert.Null(aggregate.UnitItemId);
        Assert.Null(aggregate.MeanValue);
        Assert.Equal(new KpiRagCounts(0, 1, 1, 0, 0), aggregate.RagCounts);
    }

    [Fact]
    public void AKpiWithoutAValueIsListedAndCountedAsUnknownNeverAsZero()
    {
        KpiPortfolioAggregate aggregate = KpiAggregation.Aggregate(
            [Kpi(A, Kpi1, Percent, 90m, KpiRagStatus.Amber), new KpiFigure(B, Kpi1, Percent, null, ValueStatus.Missing, KpiRagStatus.Unknown, false)], []);

        Assert.Equal((true, AggregateCoverage.Partial, 1, 90m), (aggregate.IsPartial, aggregate.Coverage, aggregate.MeasuredCount, aggregate.MeanValue));
        Assert.Equal([new AggregateExclusion(B, Kpi1, AggregateExclusionReason.ValueNotMeasured)], aggregate.Exclusions);
        Assert.Equal(new KpiRagCounts(0, 1, 0, 1, 0), aggregate.RagCounts);
    }

    private static FinancialFigures Measured(Guid project, decimal budget, decimal actual, decimal forecast) =>
        new(project, Money.CurrencyCode, new Money(budget), new Money(actual), new Money(forecast), ValueStatus.Measured, false);

    private static KpiFigure Kpi(Guid project, Guid kpi, Guid unit, decimal value, KpiRagStatus rag) => new(project, kpi, unit, value, ValueStatus.Measured, rag, false);
}
