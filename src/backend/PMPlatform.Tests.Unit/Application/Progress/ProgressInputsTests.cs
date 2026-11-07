using PMPlatform.Application.Features.Progress;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Tests.Unit.Application.Progress;

/// <summary>Where a submission's figure comes from: the breakdown when it gives one, else the declared figure, else nothing (review round 5, item 1).</summary>
public sealed class ProgressInputsTests
{
    private static readonly WorkItemProgress Leaf = new(Guid.NewGuid(), null, 10, 30);

    [Fact]
    public void TheBreakdownWinsOverADeclaredFigure() =>
        Assert.Equal<(decimal?, ProgressActualSource)>((30m, ProgressActualSource.WorkBreakdown), (ProgressInputs.None with { WorkBreakdown = [Leaf], DeclaredActualPercent = 80 }).Actual());

    [Fact]
    public void WithoutABreakdownTheDeclaredFigureStands() =>
        Assert.Equal<(decimal?, ProgressActualSource)>((80m, ProgressActualSource.Declared), (ProgressInputs.None with { DeclaredActualPercent = 80 }).Actual());

    [Fact]
    public void ABreakdownWithNoPlannedDaysGivesNoFigure() =>
        Assert.Equal<(decimal?, ProgressActualSource)>((null, ProgressActualSource.NotAvailable), (ProgressInputs.None with { WorkBreakdown = [Leaf with { PlannedDurationDays = 0 }] }).Actual());

    [Fact]
    public void NothingAtAllIsNotAvailableNeverZero() =>
        Assert.Equal<(decimal?, ProgressActualSource)>((null, ProgressActualSource.NotAvailable), ProgressInputs.None.Actual());
}
