using PMPlatform.Application.Features.Progress;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Tests.Unit.Application.Progress;

/// <summary>The review and publication workflow: four forward edges, no publication without review, two final states.</summary>
public sealed class ProgressWorkflowTests
{
    [Fact]
    public void TheWorkflowHasExactlyTheFourEdgesOfTask044()
    {
        Assert.Equal(
            [
                (ProgressSubmissionStatus.Draft, ProgressSubmissionStatus.Submitted),
                (ProgressSubmissionStatus.Submitted, ProgressSubmissionStatus.UnderReview),
                (ProgressSubmissionStatus.UnderReview, ProgressSubmissionStatus.Returned),
                (ProgressSubmissionStatus.UnderReview, ProgressSubmissionStatus.Published),
            ],
            ProgressWorkflow.Transitions.OrderBy(t => t.From).ThenBy(t => t.To));
    }

    /// <summary>Published progress remains governed (ADR-013): nothing reaches PUBLISHED but through review.</summary>
    [Fact]
    public void PublishedIsReachedOnlyFromUnderReview() =>
        Assert.Equal([ProgressSubmissionStatus.UnderReview], ProgressWorkflow.Transitions.Where(t => t.To == ProgressSubmissionStatus.Published).Select(t => t.From));

    [Theory]
    [InlineData(ProgressSubmissionStatus.Returned)]
    [InlineData(ProgressSubmissionStatus.Published)]
    public void ReturnedAndPublishedAreFinal(ProgressSubmissionStatus final) =>
        Assert.DoesNotContain(ProgressWorkflow.Transitions, t => t.From == final);

    [Fact]
    public void NoEdgeLeadsBack() =>
        Assert.All(ProgressWorkflow.Transitions, t => Assert.True(t.To > t.From, $"{t.From} → {t.To}"));
}
