using PMPlatform.Application.Common.Governance;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Tests.Unit.Application.Governance;

/// <summary>ERD D-12, the governed lifecycle primitive (TASK-034): DRAFT → VALIDATED → PUBLISHED → RETIRED, three people.</summary>
public sealed class GovernedLifecycleTests
{
    private static readonly Guid Author = Guid.Parse("00000000-0000-4000-8000-00000000a001");
    private static readonly Guid Reviewer = Guid.Parse("00000000-0000-4000-8000-00000000a002");
    private static readonly Guid Publisher = Guid.Parse("00000000-0000-4000-8000-00000000a003");
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AuthorReviewerAndPublisherAreThreePeople()
    {
        ConfigurationVersion row = Draft();

        Assert.Equal(GovernedTransitionOutcome.SeparationOfDuties, GovernedLifecycle.Validate(row, Author, Now));
        Assert.Equal(GovernedLifecycleState.Draft, row.LifecycleState);
        Assert.Equal(GovernedTransitionOutcome.Applied, GovernedLifecycle.Validate(row, Reviewer, Now));
        Assert.Equal(GovernedTransitionOutcome.SeparationOfDuties, GovernedLifecycle.Publish(row, Author, Now));
        Assert.Equal(GovernedTransitionOutcome.SeparationOfDuties, GovernedLifecycle.Publish(row, Reviewer, Now));
        Assert.Equal(GovernedLifecycleState.Validated, row.LifecycleState);
        Assert.Equal(GovernedTransitionOutcome.Applied, GovernedLifecycle.Publish(row, Publisher, Now));

        Assert.Equal(
            (GovernedLifecycleState.Published, Reviewer, Now, Publisher, Now),
            (row.LifecycleState, row.ValidatedByUserId!.Value, row.ValidatedAt!.Value, row.PublishedByUserId!.Value, row.PublishedAt!.Value));
    }

    [Fact]
    public void OnlyTheAuthorEditsADraftAndNobodyEditsAnythingElse()
    {
        ConfigurationVersion row = Draft();

        Assert.Equal(GovernedTransitionOutcome.Applied, GovernedLifecycle.CheckDraftEdit(row, Author));
        Assert.Equal(GovernedTransitionOutcome.SeparationOfDuties, GovernedLifecycle.CheckDraftEdit(row, Reviewer));
        GovernedLifecycle.Validate(row, Reviewer, Now);
        Assert.Equal(GovernedTransitionOutcome.InvalidTransition, GovernedLifecycle.CheckDraftEdit(row, Author));
        GovernedLifecycle.Publish(row, Publisher, Now);
        Assert.Equal(GovernedTransitionOutcome.InvalidTransition, GovernedLifecycle.CheckDraftEdit(row, Author));
        GovernedLifecycle.Retire(row, Now);
        Assert.Equal(GovernedTransitionOutcome.TerminalState, GovernedLifecycle.CheckDraftEdit(row, Author));
    }

    [Fact]
    public void StepsFollowTheLifecycleInOrder()
    {
        ConfigurationVersion row = Draft();

        Assert.Equal(GovernedTransitionOutcome.InvalidTransition, GovernedLifecycle.Publish(row, Publisher, Now));
        GovernedLifecycle.Validate(row, Reviewer, Now);
        Assert.Equal(GovernedTransitionOutcome.InvalidTransition, GovernedLifecycle.Validate(row, Publisher, Now));
        GovernedLifecycle.Publish(row, Publisher, Now);
        Assert.Equal(GovernedTransitionOutcome.InvalidTransition, GovernedLifecycle.Publish(row, Publisher, Now));
    }

    [Theory]
    [InlineData(GovernedLifecycleState.Draft)]
    [InlineData(GovernedLifecycleState.Validated)]
    [InlineData(GovernedLifecycleState.Published)]
    public void AnyStateButRetiredRetiresAndRetiredIsTerminal(GovernedLifecycleState state)
    {
        ConfigurationVersion row = Draft();
        row.LifecycleState = state;

        Assert.Equal(GovernedTransitionOutcome.Applied, GovernedLifecycle.Retire(row, Now));
        Assert.Equal((GovernedLifecycleState.Retired, Now), (row.LifecycleState, row.RetiredAt!.Value));
        Assert.Equal(GovernedTransitionOutcome.TerminalState, GovernedLifecycle.Retire(row, Now.AddDays(1)));
        Assert.Equal(GovernedTransitionOutcome.TerminalState, GovernedLifecycle.Validate(row, Reviewer, Now));
        Assert.Equal(GovernedTransitionOutcome.TerminalState, GovernedLifecycle.Publish(row, Publisher, Now));
        Assert.Equal(Now, row.RetiredAt);
    }

    private static ConfigurationVersion Draft() => new() { Id = Guid.NewGuid(), LifecycleState = GovernedLifecycleState.Draft, CreatedBy = Author };
}
