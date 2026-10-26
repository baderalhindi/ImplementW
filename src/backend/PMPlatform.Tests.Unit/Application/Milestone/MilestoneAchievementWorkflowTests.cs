using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Milestone;
using PMPlatform.Domain.Milestone;

namespace PMPlatform.Tests.Unit.Application.Milestone;

/// <summary>
/// An achievement revision's edges (TASK-050): accepted only by WF-11's approval, never edited once accepted, superseded only
/// from ACCEPTED, and RETURNED and SUPERSEDED final — so a correction can only be a new revision.
/// </summary>
public sealed class MilestoneAchievementWorkflowTests
{
    [Fact]
    public void TheWorkflowHasExactlyTheFourEdgesOfTask050()
    {
        Assert.Equal(
            [
                (MilestoneAchievementStatus.Draft, MilestoneAchievementStatus.Submitted),
                (MilestoneAchievementStatus.Submitted, MilestoneAchievementStatus.Returned),
                (MilestoneAchievementStatus.Submitted, MilestoneAchievementStatus.Accepted),
                (MilestoneAchievementStatus.Accepted, MilestoneAchievementStatus.Superseded),
            ],
            MilestoneAchievementWorkflow.Transitions.OrderBy(t => t.From).ThenBy(t => t.To));
    }

    /// <summary>The acceptance criterion: an ACCEPTED revision leaves ACCEPTED only by being superseded, never by being edited back to DRAFT.</summary>
    [Fact]
    public void AcceptedIsLeftOnlyForSuperseded() =>
        Assert.Equal([MilestoneAchievementStatus.Superseded], MilestoneAchievementWorkflow.Transitions.Where(t => t.From == MilestoneAchievementStatus.Accepted).Select(t => t.To));

    [Fact]
    public void AcceptedIsReachedOnlyFromSubmitted() =>
        Assert.Equal([MilestoneAchievementStatus.Submitted], MilestoneAchievementWorkflow.Transitions.Where(t => t.To == MilestoneAchievementStatus.Accepted).Select(t => t.From));

    [Fact]
    public void NoEdgeReturnsToDraft() =>
        Assert.DoesNotContain(MilestoneAchievementWorkflow.Transitions, t => t.To == MilestoneAchievementStatus.Draft);

    [Theory]
    [InlineData(MilestoneAchievementStatus.Returned)]
    [InlineData(MilestoneAchievementStatus.Superseded)]
    public void ReturnedAndSupersededAreFinal(MilestoneAchievementStatus final) =>
        Assert.DoesNotContain(MilestoneAchievementWorkflow.Transitions, t => t.From == final);

    [Theory]
    [InlineData(MilestoneAchievementStatus.Draft, true)]
    [InlineData(MilestoneAchievementStatus.Submitted, true)]
    [InlineData(MilestoneAchievementStatus.Returned, false)]
    [InlineData(MilestoneAchievementStatus.Accepted, false)]
    [InlineData(MilestoneAchievementStatus.Superseded, false)]
    public void ARevisionIsOpenUntilWf11Decides(MilestoneAchievementStatus status, bool open) =>
        Assert.Equal(open, MilestoneAchievementWorkflow.IsOpen(status));

    /// <summary>Only an approval accepts; everything else WF-11 can end a run with sends the claim back to its claimant.</summary>
    [Theory]
    [InlineData(ApprovalOutcomeDecision.Approved, MilestoneAchievementStatus.Accepted)]
    [InlineData(ApprovalOutcomeDecision.Returned, MilestoneAchievementStatus.Returned)]
    [InlineData(ApprovalOutcomeDecision.Rejected, MilestoneAchievementStatus.Returned)]
    [InlineData(ApprovalOutcomeDecision.Withdrawn, MilestoneAchievementStatus.Returned)]
    public void Wf11sDecisionAcceptsOrReturns(ApprovalOutcomeDecision decision, MilestoneAchievementStatus status)
    {
        Assert.Equal(status, MilestoneAchievementWorkflow.OutcomeOf(decision));
        Assert.True(MilestoneAchievementWorkflow.Allows(MilestoneAchievementStatus.Submitted, status));
    }

    [Fact]
    public void EveryDecisionIsMapped() =>
        Assert.All(Enum.GetValues<ApprovalOutcomeDecision>(), d => Assert.True(Enum.IsDefined(MilestoneAchievementWorkflow.OutcomeOf(d))));
}
