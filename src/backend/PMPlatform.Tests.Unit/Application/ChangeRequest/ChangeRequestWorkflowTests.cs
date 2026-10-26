using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.ChangeRequest;
using PMPlatform.Domain.ChangeRequest;

namespace PMPlatform.Tests.Unit.Application.ChangeRequest;

/// <summary>
/// The workbook row's lifecycle, Draft → Submitted → Under Review → Returned / Approved / Rejected → Implementation → Implemented →
/// Closed, with Withdrawn: every command takes one step of it, WF-11's decision takes the four steps out of review, and nothing else
/// moves a request — in particular, nothing reaches APPROVED but a review and nothing reaches IMPLEMENTED but an implementation.
/// </summary>
public sealed class ChangeRequestWorkflowTests
{
    [Fact]
    public void TheStateMachineHasTheTwelveStepsOfTheRow() =>
        Assert.Equal(
            new HashSet<(ChangeRequestStatus, ChangeRequestStatus)>
            {
                (ChangeRequestStatus.Draft, ChangeRequestStatus.Submitted), (ChangeRequestStatus.Returned, ChangeRequestStatus.Submitted),
                (ChangeRequestStatus.Submitted, ChangeRequestStatus.UnderReview), (ChangeRequestStatus.UnderReview, ChangeRequestStatus.Approved),
                (ChangeRequestStatus.UnderReview, ChangeRequestStatus.Returned), (ChangeRequestStatus.UnderReview, ChangeRequestStatus.Rejected),
                (ChangeRequestStatus.UnderReview, ChangeRequestStatus.Withdrawn), (ChangeRequestStatus.Submitted, ChangeRequestStatus.Withdrawn),
                (ChangeRequestStatus.Returned, ChangeRequestStatus.Withdrawn), (ChangeRequestStatus.Approved, ChangeRequestStatus.Implementation),
                (ChangeRequestStatus.Implementation, ChangeRequestStatus.Implemented), (ChangeRequestStatus.Implemented, ChangeRequestStatus.Closed),
            },
            ChangeRequestWorkflow.Transitions);

    /// <summary>The commands and WF-11's four outcomes take exactly the state machine's steps, and each command one of them.</summary>
    [Fact]
    public void TheCommandsAndTheReviewOutcomesTakeExactlyTheStateMachinesSteps()
    {
        HashSet<(ChangeRequestStatus, ChangeRequestStatus)> taken =
            [.. Enum.GetValues<ApprovalOutcomeDecision>().Select(d => (ChangeRequestStatus.UnderReview, ChangeRequestWorkflow.OutcomeOf(d)))];
        foreach (ChangeRequestCommand command in Enum.GetValues<ChangeRequestCommand>())
        {
            foreach (ChangeRequestStatus from in Enum.GetValues<ChangeRequestStatus>())
            {
                if (ChangeRequestWorkflow.TargetOf(command, from) is { } to)
                {
                    Assert.Contains((from, to), ChangeRequestWorkflow.Transitions);
                    taken.Add((from, to));
                }
            }
        }

        Assert.True(taken.SetEquals(ChangeRequestWorkflow.Transitions));
    }

    [Fact]
    public void WfElevenDecidesTheReview()
    {
        Assert.Equal(ChangeRequestStatus.Approved, ChangeRequestWorkflow.OutcomeOf(ApprovalOutcomeDecision.Approved));
        Assert.Equal(ChangeRequestStatus.Returned, ChangeRequestWorkflow.OutcomeOf(ApprovalOutcomeDecision.Returned));
        Assert.Equal(ChangeRequestStatus.Rejected, ChangeRequestWorkflow.OutcomeOf(ApprovalOutcomeDecision.Rejected));
        Assert.Equal(ChangeRequestStatus.Withdrawn, ChangeRequestWorkflow.OutcomeOf(ApprovalOutcomeDecision.Withdrawn));
    }

    /// <summary>
    /// The acceptance criterion's shape in the machine: APPROVED is entered from UNDER_REVIEW alone, and IMPLEMENTED from IMPLEMENTATION
    /// alone, which only APPROVED enters — so no path implements a change that was not approved, or approves one not reviewed.
    /// </summary>
    [Fact]
    public void NoPathSkipsReviewOrImplementation()
    {
        Assert.Equal([ChangeRequestStatus.UnderReview], ChangeRequestWorkflow.Transitions.Where(t => t.To == ChangeRequestStatus.Approved).Select(t => t.From));
        Assert.Equal([ChangeRequestStatus.Approved], ChangeRequestWorkflow.Transitions.Where(t => t.To == ChangeRequestStatus.Implementation).Select(t => t.From));
        Assert.Equal([ChangeRequestStatus.Implementation], ChangeRequestWorkflow.Transitions.Where(t => t.To == ChangeRequestStatus.Implemented).Select(t => t.From));
        Assert.DoesNotContain(Reachable(ChangeRequestStatus.Draft, without: ChangeRequestStatus.UnderReview), s => s == ChangeRequestStatus.Approved);
        Assert.DoesNotContain(Reachable(ChangeRequestStatus.Draft, without: ChangeRequestStatus.Approved), s => s == ChangeRequestStatus.Implemented);
    }

    /// <summary>REJECTED, WITHDRAWN and CLOSED take no command and leave by no step: a later proposal is a new request (BR-CHG-015).</summary>
    [Theory]
    [InlineData(ChangeRequestStatus.Rejected)]
    [InlineData(ChangeRequestStatus.Withdrawn)]
    [InlineData(ChangeRequestStatus.Closed)]
    public void FinalStatesAreFinal(ChangeRequestStatus status)
    {
        Assert.True(ChangeRequestWorkflow.IsFinal(status));
        Assert.All(Enum.GetValues<ChangeRequestCommand>(), command => Assert.Null(ChangeRequestWorkflow.TargetOf(command, status)));
        Assert.DoesNotContain(ChangeRequestWorkflow.Transitions, t => t.From == status);
    }

    /// <summary>A request is edited only while it is with its requester, and previewed until its review records its materiality.</summary>
    [Fact]
    public void ARequestIsEditableWithItsRequesterAndPreviewedBeforeReview()
    {
        Assert.Equal([ChangeRequestStatus.Draft, ChangeRequestStatus.Returned], Enum.GetValues<ChangeRequestStatus>().Where(ChangeRequestWorkflow.IsEditable));
        Assert.Equal(
            [ChangeRequestStatus.Draft, ChangeRequestStatus.Submitted, ChangeRequestStatus.Returned],
            Enum.GetValues<ChangeRequestStatus>().Where(ChangeRequestWorkflow.IsBeforeReview));
    }

    private static HashSet<ChangeRequestStatus> Reachable(ChangeRequestStatus from, ChangeRequestStatus without)
    {
        HashSet<ChangeRequestStatus> seen = [from];
        Queue<ChangeRequestStatus> next = new([from]);
        while (next.TryDequeue(out ChangeRequestStatus status))
        {
            foreach ((_, ChangeRequestStatus to) in ChangeRequestWorkflow.Transitions.Where(t => t.From == status && t.To != without))
            {
                if (seen.Add(to))
                {
                    next.Enqueue(to);
                }
            }
        }

        return seen;
    }
}
