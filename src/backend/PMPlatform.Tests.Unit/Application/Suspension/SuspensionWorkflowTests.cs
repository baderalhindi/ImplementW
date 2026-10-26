using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Suspension;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Tests.Unit.Application.Suspension;

/// <summary>
/// WF-09's request lifecycle (TASK-062): Draft → Submitted → Under Review → Returned / Approved / Rejected, Withdrawn, and Approved →
/// Effected. Every command takes one step of it, WF-11's decision takes the four steps out of review, and nothing else moves a request —
/// in particular, the approval and the lifecycle activation are two steps, never one.
/// </summary>
public sealed class SuspensionWorkflowTests
{
    [Fact]
    public void TheStateMachineHasItsTenSteps() =>
        Assert.Equal(
            new HashSet<(SuspensionRequestStatus, SuspensionRequestStatus)>
            {
                (SuspensionRequestStatus.Draft, SuspensionRequestStatus.Submitted), (SuspensionRequestStatus.Returned, SuspensionRequestStatus.Submitted),
                (SuspensionRequestStatus.Submitted, SuspensionRequestStatus.UnderReview), (SuspensionRequestStatus.UnderReview, SuspensionRequestStatus.Approved),
                (SuspensionRequestStatus.UnderReview, SuspensionRequestStatus.Returned), (SuspensionRequestStatus.UnderReview, SuspensionRequestStatus.Rejected),
                (SuspensionRequestStatus.UnderReview, SuspensionRequestStatus.Withdrawn), (SuspensionRequestStatus.Submitted, SuspensionRequestStatus.Withdrawn),
                (SuspensionRequestStatus.Returned, SuspensionRequestStatus.Withdrawn), (SuspensionRequestStatus.Approved, SuspensionRequestStatus.Effected),
            },
            SuspensionWorkflow.Transitions);

    /// <summary>The commands and WF-11's four outcomes take exactly the state machine's steps, and each command one of them.</summary>
    [Fact]
    public void TheCommandsAndTheReviewOutcomesTakeExactlyTheStateMachinesSteps()
    {
        HashSet<(SuspensionRequestStatus, SuspensionRequestStatus)> taken =
            [.. Enum.GetValues<ApprovalOutcomeDecision>().Select(d => (SuspensionRequestStatus.UnderReview, SuspensionWorkflow.OutcomeOf(d)))];
        foreach (SuspensionCommand command in Enum.GetValues<SuspensionCommand>())
        {
            foreach (SuspensionRequestStatus from in Enum.GetValues<SuspensionRequestStatus>())
            {
                if (SuspensionWorkflow.TargetOf(command, from) is { } to)
                {
                    Assert.Contains((from, to), SuspensionWorkflow.Transitions);
                    taken.Add((from, to));
                }
            }
        }

        Assert.True(taken.SetEquals(SuspensionWorkflow.Transitions));
    }

    /// <summary>
    /// Acceptance criterion 3 in the machine: WF-11's decision never effects a request, and only the activation command does, from APPROVED
    /// alone — so approval completion and the lifecycle activation are always two separate moves.
    /// </summary>
    [Fact]
    public void ApprovalAndActivationAreTwoSteps()
    {
        Assert.DoesNotContain(SuspensionRequestStatus.Effected, Enum.GetValues<ApprovalOutcomeDecision>().Select(SuspensionWorkflow.OutcomeOf));
        Assert.Equal([SuspensionRequestStatus.Approved], SuspensionWorkflow.Transitions.Where(t => t.To == SuspensionRequestStatus.Effected).Select(t => t.From));
        Assert.Equal([SuspensionRequestStatus.UnderReview], SuspensionWorkflow.Transitions.Where(t => t.To == SuspensionRequestStatus.Approved).Select(t => t.From));
        Assert.Equal(
            [SuspensionCommand.Activate],
            Enum.GetValues<SuspensionCommand>().Where(c => Enum.GetValues<SuspensionRequestStatus>().Any(s => SuspensionWorkflow.TargetOf(c, s) == SuspensionRequestStatus.Effected)));
    }

    [Theory]
    [InlineData(SuspensionRequestStatus.Draft, true, false)]
    [InlineData(SuspensionRequestStatus.Returned, true, false)]
    [InlineData(SuspensionRequestStatus.Submitted, false, false)]
    [InlineData(SuspensionRequestStatus.UnderReview, false, false)]
    [InlineData(SuspensionRequestStatus.Approved, false, false)]
    [InlineData(SuspensionRequestStatus.Rejected, false, true)]
    [InlineData(SuspensionRequestStatus.Withdrawn, false, true)]
    [InlineData(SuspensionRequestStatus.Effected, false, true)]
    public void OnlyARequestWithItsRequesterIsEditedAndTheFinalStatesChangeNoMore(SuspensionRequestStatus status, bool editable, bool final)
    {
        Assert.Equal(editable, SuspensionWorkflow.IsEditable(status));
        Assert.Equal(final, SuspensionWorkflow.IsFinal(status));
        Assert.Equal(final, !SuspensionWorkflow.Transitions.Any(t => t.From == status));
    }
}
