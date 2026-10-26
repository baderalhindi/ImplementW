using PMPlatform.Application.Features.Schedule;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Tests.Unit.Application.Schedule;

/// <summary>The baseline's edges: ACTIVE only by submission or WF-11's approval, SUPERSEDED only from ACTIVE, three final states.</summary>
public sealed class BaselineWorkflowTests
{
    [Fact]
    public void TheWorkflowHasExactlyTheNineEdgesOfTask046()
    {
        Assert.Equal(
            [
                (ProjectBaselineStatus.Draft, ProjectBaselineStatus.Submitted),
                (ProjectBaselineStatus.Draft, ProjectBaselineStatus.Active),
                (ProjectBaselineStatus.Submitted, ProjectBaselineStatus.Returned),
                (ProjectBaselineStatus.Submitted, ProjectBaselineStatus.Active),
                (ProjectBaselineStatus.Submitted, ProjectBaselineStatus.Rejected),
                (ProjectBaselineStatus.Submitted, ProjectBaselineStatus.Withdrawn),
                (ProjectBaselineStatus.Returned, ProjectBaselineStatus.Submitted),
                (ProjectBaselineStatus.Returned, ProjectBaselineStatus.Active),
                (ProjectBaselineStatus.Active, ProjectBaselineStatus.Superseded),
            ],
            BaselineWorkflow.Transitions.OrderBy(t => t.From).ThenBy(t => t.To));
    }

    /// <summary>An ACTIVE baseline leaves ACTIVE only by being superseded: it is never edited, returned or withdrawn.</summary>
    [Fact]
    public void ActiveIsLeftOnlyForSuperseded() =>
        Assert.Equal([ProjectBaselineStatus.Superseded], BaselineWorkflow.Transitions.Where(t => t.From == ProjectBaselineStatus.Active).Select(t => t.To));

    [Fact]
    public void SupersededIsReachedOnlyFromActive() =>
        Assert.Equal([ProjectBaselineStatus.Active], BaselineWorkflow.Transitions.Where(t => t.To == ProjectBaselineStatus.Superseded).Select(t => t.From));

    [Theory]
    [InlineData(ProjectBaselineStatus.Superseded)]
    [InlineData(ProjectBaselineStatus.Rejected)]
    [InlineData(ProjectBaselineStatus.Withdrawn)]
    public void SupersededRejectedAndWithdrawnAreFinal(ProjectBaselineStatus final) =>
        Assert.DoesNotContain(BaselineWorkflow.Transitions, t => t.From == final);

    [Theory]
    [InlineData(ProjectBaselineStatus.Draft, true)]
    [InlineData(ProjectBaselineStatus.Submitted, true)]
    [InlineData(ProjectBaselineStatus.Returned, true)]
    [InlineData(ProjectBaselineStatus.Active, false)]
    [InlineData(ProjectBaselineStatus.Superseded, false)]
    [InlineData(ProjectBaselineStatus.Rejected, false)]
    [InlineData(ProjectBaselineStatus.Withdrawn, false)]
    public void ACandidateIsOpenUntilItsWorkflowEnds(ProjectBaselineStatus status, bool open) => Assert.Equal(open, BaselineWorkflow.IsOpen(status));
}
