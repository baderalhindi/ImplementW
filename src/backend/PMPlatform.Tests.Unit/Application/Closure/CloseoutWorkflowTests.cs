using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Closure;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Tests.Unit.Application.Closure;

/// <summary>
/// WF-10's case lifecycle (TASK-063; WF-10 §4.1, §4.2, Appendix A): Draft → Submitted → Under Review → Returned / Approved / Rejected,
/// Withdrawn, and Approved → Effected. Every command takes one step of it or keeps a case as it is, WF-11's decision takes the four steps out
/// of review, and the approval and the lifecycle activation are two steps, never one.
/// </summary>
public sealed class CloseoutWorkflowTests
{
    [Fact]
    public void TheStateMachineHasItsElevenSteps() =>
        Assert.Equal(
            new HashSet<(CloseoutCaseStatus, CloseoutCaseStatus)>
            {
                (CloseoutCaseStatus.Draft, CloseoutCaseStatus.Submitted), (CloseoutCaseStatus.Returned, CloseoutCaseStatus.Submitted),
                (CloseoutCaseStatus.Submitted, CloseoutCaseStatus.UnderReview), (CloseoutCaseStatus.UnderReview, CloseoutCaseStatus.Approved),
                (CloseoutCaseStatus.UnderReview, CloseoutCaseStatus.Returned), (CloseoutCaseStatus.UnderReview, CloseoutCaseStatus.Rejected),
                (CloseoutCaseStatus.UnderReview, CloseoutCaseStatus.Withdrawn), (CloseoutCaseStatus.Draft, CloseoutCaseStatus.Withdrawn),
                (CloseoutCaseStatus.Submitted, CloseoutCaseStatus.Withdrawn), (CloseoutCaseStatus.Returned, CloseoutCaseStatus.Withdrawn),
                (CloseoutCaseStatus.Approved, CloseoutCaseStatus.Effected),
            },
            CloseoutWorkflow.Transitions);

    /// <summary>The commands and WF-11's four outcomes take exactly the state machine's steps; evaluation and waiver keep the status.</summary>
    [Fact]
    public void TheCommandsAndTheReviewOutcomesTakeExactlyTheStateMachinesSteps()
    {
        HashSet<(CloseoutCaseStatus, CloseoutCaseStatus)> taken =
            [.. Enum.GetValues<ApprovalOutcomeDecision>().Select(d => (CloseoutCaseStatus.UnderReview, CloseoutWorkflow.OutcomeOf(d)))];
        foreach (CloseoutCommand command in Enum.GetValues<CloseoutCommand>())
        {
            foreach (CloseoutCaseStatus from in Enum.GetValues<CloseoutCaseStatus>())
            {
                if (CloseoutWorkflow.TargetOf(command, from) is not { } to)
                {
                    continue;
                }

                if (command is CloseoutCommand.EvaluateReadiness or CloseoutCommand.WaiveCheck)
                {
                    Assert.Equal((from, true), (to, CloseoutWorkflow.IsEditable(from)));
                }
                else
                {
                    Assert.Contains((from, to), CloseoutWorkflow.Transitions);
                    taken.Add((from, to));
                }
            }
        }

        Assert.True(taken.SetEquals(CloseoutWorkflow.Transitions));
    }

    /// <summary>
    /// WF-10 P3, CLO-CC-10: WF-11's decision never effects a case, and only the activation command does, from APPROVED alone — so the
    /// approval and the project's transition are always two separate moves.
    /// </summary>
    [Fact]
    public void ApprovalAndActivationAreTwoSteps()
    {
        Assert.DoesNotContain(CloseoutCaseStatus.Effected, Enum.GetValues<ApprovalOutcomeDecision>().Select(CloseoutWorkflow.OutcomeOf));
        Assert.Equal([CloseoutCaseStatus.Approved], CloseoutWorkflow.Transitions.Where(t => t.To == CloseoutCaseStatus.Effected).Select(t => t.From));
        Assert.Equal([CloseoutCaseStatus.UnderReview], CloseoutWorkflow.Transitions.Where(t => t.To == CloseoutCaseStatus.Approved).Select(t => t.From));
        Assert.Equal(
            [CloseoutCommand.Activate],
            Enum.GetValues<CloseoutCommand>().Where(c => Enum.GetValues<CloseoutCaseStatus>().Any(s => CloseoutWorkflow.TargetOf(c, s) == CloseoutCaseStatus.Effected)));
    }

    [Theory]
    [InlineData(CloseoutCaseStatus.Draft, true, false)]
    [InlineData(CloseoutCaseStatus.Returned, true, false)]
    [InlineData(CloseoutCaseStatus.Submitted, false, false)]
    [InlineData(CloseoutCaseStatus.UnderReview, false, false)]
    [InlineData(CloseoutCaseStatus.Approved, false, false)]
    [InlineData(CloseoutCaseStatus.Rejected, false, true)]
    [InlineData(CloseoutCaseStatus.Withdrawn, false, true)]
    [InlineData(CloseoutCaseStatus.Effected, false, true)]
    public void OnlyACaseWithItsRequesterIsEditedAndTheFinalStatesChangeNoMore(CloseoutCaseStatus status, bool editable, bool final)
    {
        Assert.Equal(editable, CloseoutWorkflow.IsEditable(status));
        Assert.Equal(final, CloseoutWorkflow.IsFinal(status));
        Assert.Equal(final, !CloseoutWorkflow.Transitions.Any(t => t.From == status));
    }

    /// <summary>An obligation is OPEN, then IN_PROGRESS, and settled once as SATISFIED, CANCELLED or WAIVED.</summary>
    [Fact]
    public void AnObligationIsSettledOnceFromOpenOrInProgress()
    {
        HashSet<(PostProjectObligationStatus, PostProjectObligationStatus)> steps =
        [
            .. from command in Enum.GetValues<ObligationCommand>()
               from status in Enum.GetValues<PostProjectObligationStatus>()
               let to = PostProjectObligationRules.TargetOf(command, status)
               where to is not null
               select (status, to.Value),
        ];

        HashSet<(PostProjectObligationStatus, PostProjectObligationStatus)> expected =
        [
            (PostProjectObligationStatus.Open, PostProjectObligationStatus.InProgress),
            (PostProjectObligationStatus.Open, PostProjectObligationStatus.Satisfied), (PostProjectObligationStatus.InProgress, PostProjectObligationStatus.Satisfied),
            (PostProjectObligationStatus.Open, PostProjectObligationStatus.Cancelled), (PostProjectObligationStatus.InProgress, PostProjectObligationStatus.Cancelled),
            (PostProjectObligationStatus.Open, PostProjectObligationStatus.Waived), (PostProjectObligationStatus.InProgress, PostProjectObligationStatus.Waived),
        ];
        Assert.Equal(expected, steps);
        Assert.Equal(
            [PostProjectObligationStatus.Open, PostProjectObligationStatus.InProgress],
            Enum.GetValues<PostProjectObligationStatus>().Where(PostProjectObligationRules.IsOpen));
    }
}
