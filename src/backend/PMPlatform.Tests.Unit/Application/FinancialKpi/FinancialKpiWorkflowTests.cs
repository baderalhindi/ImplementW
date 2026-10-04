using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.FinancialKpi;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Tests.Unit.Application.FinancialKpi;

/// <summary>The WF-14 workflows: approved versions through WF-11, a period's update through AHDA's review, a measurement's publication.</summary>
public sealed class FinancialKpiWorkflowTests
{
    [Fact]
    public void AnApprovedVersionHasExactlyItsSevenEdges()
    {
        Assert.Equal(
            [
                (ApprovedVersionStatus.Draft, ApprovedVersionStatus.Submitted),
                (ApprovedVersionStatus.Submitted, ApprovedVersionStatus.Returned),
                (ApprovedVersionStatus.Submitted, ApprovedVersionStatus.Active),
                (ApprovedVersionStatus.Submitted, ApprovedVersionStatus.Rejected),
                (ApprovedVersionStatus.Submitted, ApprovedVersionStatus.Withdrawn),
                (ApprovedVersionStatus.Returned, ApprovedVersionStatus.Submitted),
                (ApprovedVersionStatus.Active, ApprovedVersionStatus.Superseded),
            ],
            ApprovedVersionWorkflow.Transitions.OrderBy(t => t.From).ThenBy(t => t.To));
    }

    /// <summary>An approved target or budget is never edited: nothing leads out of ACTIVE but SUPERSEDED, and nothing out of the ends.</summary>
    [Theory]
    [InlineData(ApprovedVersionStatus.Superseded)]
    [InlineData(ApprovedVersionStatus.Rejected)]
    [InlineData(ApprovedVersionStatus.Withdrawn)]
    public void TheEndsAreFinal(ApprovedVersionStatus final) => Assert.DoesNotContain(ApprovedVersionWorkflow.Transitions, t => t.From == final);

    [Fact]
    public void OnlyWf11sApprovalActivates() =>
        Assert.Equal([ApprovedVersionStatus.Submitted], ApprovedVersionWorkflow.Transitions.Where(t => t.To == ApprovedVersionStatus.Active).Select(t => t.From));

    [Theory]
    [InlineData(ApprovalOutcomeDecision.Approved, ApprovedVersionStatus.Active)]
    [InlineData(ApprovalOutcomeDecision.Returned, ApprovedVersionStatus.Returned)]
    [InlineData(ApprovalOutcomeDecision.Rejected, ApprovedVersionStatus.Rejected)]
    [InlineData(ApprovalOutcomeDecision.Withdrawn, ApprovedVersionStatus.Withdrawn)]
    public void EachDecisionHasItsState(ApprovalOutcomeDecision decision, ApprovedVersionStatus expected) =>
        Assert.Equal(expected, ApprovedVersionWorkflow.OutcomeOf(decision));

    [Fact]
    public void OnlyDraftAndReturnedAreEditable() =>
        Assert.Equal([ApprovedVersionStatus.Draft, ApprovedVersionStatus.Returned], Enum.GetValues<ApprovedVersionStatus>().Where(ApprovedVersionWorkflow.IsEditable));

    /// <summary>Published financial figures remain governed (ADR-013): nothing reaches PUBLISHED but through AHDA's review.</summary>
    [Fact]
    public void AFinancialUpdateIsPublishedOnlyFromReview()
    {
        Assert.Equal(4, FinancialUpdateWorkflow.Transitions.Count);
        Assert.Equal([FinancialUpdateStatus.UnderReview], FinancialUpdateWorkflow.Transitions.Where(t => t.To == FinancialUpdateStatus.Published).Select(t => t.From));
        Assert.DoesNotContain(FinancialUpdateWorkflow.Transitions, t => t.From is FinancialUpdateStatus.Returned or FinancialUpdateStatus.Published);
    }

    [Fact]
    public void AMeasurementMovesForwardOnlyToPublished()
    {
        Assert.Equal(
            [(KpiMeasurementStatus.Draft, KpiMeasurementStatus.Submitted), (KpiMeasurementStatus.Submitted, KpiMeasurementStatus.Published)],
            KpiMeasurementWorkflow.Transitions.OrderBy(t => t.From));
    }

    [Fact]
    public void ARetiredAssignmentIsFinal()
    {
        Assert.DoesNotContain(KpiAssignmentWorkflow.Transitions, t => t.From == KpiAssignmentStatus.Retired);
        Assert.True(KpiAssignmentWorkflow.Allows(KpiAssignmentStatus.Suspended, KpiAssignmentStatus.Active));
    }
}
