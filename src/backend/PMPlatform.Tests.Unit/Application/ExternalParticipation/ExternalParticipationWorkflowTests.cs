using PMPlatform.Application.Features.ExternalParticipation;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Tests.Unit.Application.ExternalParticipation;

/// <summary>
/// WF-13's two state machines (TASK-066). A revision moves DRAFT → SUBMITTED → UNDER_REVIEW and is then decided as a whole — accepted,
/// returned or rejected — never sent back to DRAFT and never reopened: a correction is the next revision. Acceptance is APPLIED at once only
/// for a reference-only answer.
/// </summary>
public sealed class ExternalParticipationWorkflowTests
{
    [Fact]
    public void TheRequestHasItsSevenSteps() =>
        Assert.Equal(
            new HashSet<(ExternalUpdateRequestStatus, ExternalUpdateRequestStatus)>
            {
                (ExternalUpdateRequestStatus.Draft, ExternalUpdateRequestStatus.Issued),
                (ExternalUpdateRequestStatus.Issued, ExternalUpdateRequestStatus.InProgress),
                (ExternalUpdateRequestStatus.InProgress, ExternalUpdateRequestStatus.Responded),
                (ExternalUpdateRequestStatus.Responded, ExternalUpdateRequestStatus.InProgress),
                (ExternalUpdateRequestStatus.Responded, ExternalUpdateRequestStatus.Closed),
                (ExternalUpdateRequestStatus.Issued, ExternalUpdateRequestStatus.Cancelled),
                (ExternalUpdateRequestStatus.InProgress, ExternalUpdateRequestStatus.Cancelled),
            },
            ExternalParticipationWorkflow.RequestTransitions);

    [Fact]
    public void TheRevisionHasItsEightSteps() =>
        Assert.Equal(
            new HashSet<(ExternalContributionStatus, ExternalContributionStatus)>
            {
                (ExternalContributionStatus.Draft, ExternalContributionStatus.Submitted),
                (ExternalContributionStatus.Submitted, ExternalContributionStatus.UnderReview),
                (ExternalContributionStatus.UnderReview, ExternalContributionStatus.Returned),
                (ExternalContributionStatus.UnderReview, ExternalContributionStatus.Rejected),
                (ExternalContributionStatus.UnderReview, ExternalContributionStatus.AcceptedPendingApplication),
                (ExternalContributionStatus.UnderReview, ExternalContributionStatus.Applied),
                (ExternalContributionStatus.AcceptedPendingApplication, ExternalContributionStatus.Applied),
                (ExternalContributionStatus.AcceptedPendingApplication, ExternalContributionStatus.ApplicationFailed),
            },
            ExternalParticipationWorkflow.ContributionTransitions);

    /// <summary>Every command takes one step of the revision's machine, and none leads back to DRAFT: what was submitted is never reopened for editing.</summary>
    [Theory]
    [InlineData(ContributionApplicationMode.ReferenceOnly)]
    [InlineData(ContributionApplicationMode.UpdateAllowedSourceFields)]
    public void TheCommandsTakeOnlyTheMachinesStepsAndNoneReopensASubmission(ContributionApplicationMode mode)
    {
        foreach (ContributionCommand command in Enum.GetValues<ContributionCommand>())
        {
            foreach (ExternalContributionStatus from in Enum.GetValues<ExternalContributionStatus>())
            {
                AdministrationResult<StatusOf> next = ExternalParticipationWorkflow.TargetOf(command, from, mode);
                if (next.Succeeded)
                {
                    Assert.Contains((from, next.Value.Status), ExternalParticipationWorkflow.ContributionTransitions);
                    Assert.NotEqual(ExternalContributionStatus.Draft, next.Value.Status);
                }
            }
        }
    }

    /// <summary>
    /// TASK-066 acceptance criterion 2 in the machine: a submitted revision is decided only from UNDER_REVIEW, as Accept, Return or Reject, and
    /// a decided one takes no decision again (409 CONTRIBUTION_ALREADY_DECIDED).
    /// </summary>
    [Fact]
    public void ARevisionIsDecidedOnceAndOnlyAsAcceptReturnOrReject()
    {
        ContributionCommand[] decisions = [ContributionCommand.Accept, ContributionCommand.Return, ContributionCommand.Reject];
        foreach (ContributionCommand decision in decisions)
        {
            Assert.True(ExternalParticipationWorkflow.TargetOf(decision, ExternalContributionStatus.UnderReview, ContributionApplicationMode.UpdateAllowedSourceFields).Succeeded);
            Assert.Equal(AdministrationErrorKind.InvalidTransition,
                ExternalParticipationWorkflow.TargetOf(decision, ExternalContributionStatus.Submitted, ContributionApplicationMode.UpdateAllowedSourceFields).Error!.Kind);
            foreach (ExternalContributionStatus decided in new[]
                     {
                         ExternalContributionStatus.Returned, ExternalContributionStatus.Rejected, ExternalContributionStatus.AcceptedPendingApplication,
                         ExternalContributionStatus.Applied, ExternalContributionStatus.ApplicationFailed,
                     })
            {
                Assert.Equal(ExternalParticipationErrorCodes.ContributionAlreadyDecided,
                    ExternalParticipationWorkflow.TargetOf(decision, decided, ContributionApplicationMode.UpdateAllowedSourceFields).Error!.Code);
            }
        }

        Assert.Equal(ExternalParticipationErrorCodes.ContributionAlreadySubmitted,
            ExternalParticipationWorkflow.TargetOf(ContributionCommand.Submit, ExternalContributionStatus.Submitted, ContributionApplicationMode.ReferenceOnly).Error!.Code);
    }

    /// <summary>WF-13 §4.5 step 8, EXT-P-07: acceptance awaits application, unless the answer is reference-only and applies to no source.</summary>
    [Fact]
    public void AcceptanceIsApplicationOnlyForAReferenceOnlyAnswer()
    {
        Assert.Equal(ExternalContributionStatus.Applied,
            ExternalParticipationWorkflow.TargetOf(ContributionCommand.Accept, ExternalContributionStatus.UnderReview, ContributionApplicationMode.ReferenceOnly).Value!.Status);
        Assert.Equal(ExternalContributionStatus.AcceptedPendingApplication,
            ExternalParticipationWorkflow.TargetOf(ContributionCommand.Accept, ExternalContributionStatus.UnderReview, ContributionApplicationMode.UpdateAllowedSourceFields).Value!.Status);
    }

    /// <summary>EXT-F-020, BR-EXT-035: due while the entity owes the answer, and only by its own date.</summary>
    [Theory]
    [InlineData(ExternalUpdateRequestStatus.Issued, 0, ResponseDueCondition.Due)]
    [InlineData(ExternalUpdateRequestStatus.Issued, 3, ResponseDueCondition.NotDue)]
    [InlineData(ExternalUpdateRequestStatus.InProgress, -1, ResponseDueCondition.Overdue)]
    [InlineData(ExternalUpdateRequestStatus.Responded, -1, ResponseDueCondition.NotApplicable)]
    [InlineData(ExternalUpdateRequestStatus.Draft, -1, ResponseDueCondition.NotApplicable)]
    [InlineData(ExternalUpdateRequestStatus.Closed, -1, ResponseDueCondition.NotApplicable)]
    public void AnAnswerIsDueOnItsDateWhileTheEntityOwesIt(ExternalUpdateRequestStatus status, int dueInDays, ResponseDueCondition expected)
    {
        DateOnly today = new(2026, 10, 9);
        Assert.Equal(expected, ExternalParticipationViews.DueConditionOf(status, today.AddDays(dueInDays), today));
        Assert.Equal(ResponseDueCondition.NotApplicable, ExternalParticipationViews.DueConditionOf(status, null, today));
    }
}
