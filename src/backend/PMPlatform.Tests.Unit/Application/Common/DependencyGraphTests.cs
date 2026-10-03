using PMPlatform.Application.Common.Graphs;

namespace PMPlatform.Tests.Unit.Application.Common;

/// <summary>VAL-SCH-008 and TASK-048: a dependency network stays acyclic, and the forward pass visits every predecessor first.</summary>
public sealed class DependencyGraphTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid D = Guid.NewGuid();

    [Fact]
    public void ADependencyOnItselfIsACycle() => Assert.True(DependencyGraph.ClosesCycle([], A, A));

    [Fact]
    public void TheReverseOfAnEdgeIsACycle() => Assert.True(DependencyGraph.ClosesCycle([(A, B)], B, A));

    /// <summary>The workbook's check: A → B → C, and C → A would close the circle.</summary>
    [Fact]
    public void AnEdgeBackToAnAncestorIsACycle() => Assert.True(DependencyGraph.ClosesCycle([(A, B), (B, C)], C, A));

    [Fact]
    public void ACycleIsFoundThroughEveryBranch() => Assert.True(DependencyGraph.ClosesCycle([(A, B), (A, C), (C, D)], D, A));

    /// <summary>A diamond joins two paths but closes nothing.</summary>
    [Fact]
    public void ADiamondIsNoCycle()
    {
        Assert.False(DependencyGraph.ClosesCycle([(A, B), (A, C), (B, D)], C, D));
        Assert.False(DependencyGraph.ClosesCycle([(A, B), (B, C)], A, C));
    }

    [Fact]
    public void AnUnrelatedEdgeIsNoCycle() => Assert.False(DependencyGraph.ClosesCycle([(A, B)], C, D));

    [Fact]
    public void TheOrderPutsEveryPredecessorFirstAndKeepsTiesAsGiven()
    {
        IReadOnlyList<Guid> order = DependencyGraph.TopologicalOrder([D, C, B, A], [(A, B), (B, C)]);

        Assert.Equal([D, A, B, C], order);
    }

    [Fact]
    public void EdgesToNodesOutsideTheSetAreIgnored() =>
        Assert.Equal([A, B], DependencyGraph.TopologicalOrder([A, B], [(C, A), (B, D)]));

    [Fact]
    public void ACycleCannotBeOrdered() =>
        Assert.Throws<InvalidOperationException>(() => DependencyGraph.TopologicalOrder([A, B], [(A, B), (B, A)]));
}
