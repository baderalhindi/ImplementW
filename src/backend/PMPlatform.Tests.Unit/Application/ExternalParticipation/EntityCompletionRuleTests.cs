using PMPlatform.Application.Features.ExternalParticipation;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Tests.Unit.Application.ExternalParticipation;

/// <summary>Review round 5, items 9 and 10: accepted over issued, per entity, with the simple average of the entities' rates.</summary>
public sealed class EntityCompletionRuleTests
{
    private static readonly DateOnly Today = new(2026, 11, 7);
    private static readonly Guid A = Guid.Parse("00000000-0000-4000-8000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-4000-8000-00000000000b");

    private static RequestCompletionFacts Request(Guid entity, ExternalContributionStatus? latest = null, bool submitted = false, DateOnly? due = null) =>
        new(Guid.NewGuid(), entity, due, submitted, latest);

    [Fact]
    public void ARateIsAcceptedOverIssuedAndTheLowestComesFirst()
    {
        List<EntityCompletionRow> rows = EntityCompletionRule.Rows(
        [
            Request(A, ExternalContributionStatus.Applied, true),
            Request(A, ExternalContributionStatus.AcceptedPendingApplication, true),
            Request(A, ExternalContributionStatus.Submitted, true),
            Request(A),
            Request(B, ExternalContributionStatus.Rejected, true),
        ], Today);

        Assert.Equal(new EntityCompletionRow(B, 1, 1, 0, 0, 0m), rows[0]);
        Assert.Equal(new EntityCompletionRow(A, 4, 3, 2, 0, 50m), rows[1]);
    }

    [Fact]
    public void TheAverageIsSimpleNotWeightedByRequestCount()
    {
        decimal? average = EntityCompletionRule.Average(
        [
            Request(A, ExternalContributionStatus.Applied, true),
            Request(B, ExternalContributionStatus.Applied, true),
            Request(B),
            Request(B),
            Request(B),
        ], Today);

        Assert.Equal(62.5m, average);
    }

    [Fact]
    public void NothingIssuedHasNoAverageNotZero() => Assert.Null(EntityCompletionRule.Average([], Today));

    [Fact]
    public void AnUnansweredRequestPastItsDueDateIsOverdue()
    {
        EntityCompletionRow row = Assert.Single(EntityCompletionRule.Rows(
        [
            Request(A, due: Today.AddDays(-1)),
            Request(A, due: Today),
            Request(A, ExternalContributionStatus.Submitted, true, Today.AddDays(-1)),
        ], Today));

        Assert.Equal(1, row.Overdue);
    }

    [Theory]
    [InlineData(CompletionPeriod.ThisMonth, "2026-11-01")]
    [InlineData(CompletionPeriod.LastThreeMonths, "2026-09-01")]
    [InlineData(CompletionPeriod.ThisYear, "2026-01-01")]
    public void APeriodStartsWhereItsNameSays(CompletionPeriod period, string start) =>
        Assert.Equal(DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), EntityCompletionRule.PeriodStart(period, Today));
}
