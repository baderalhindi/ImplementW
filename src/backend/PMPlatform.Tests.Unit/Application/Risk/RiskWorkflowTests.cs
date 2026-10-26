using System.Text.Json;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Risk;
using PMPlatform.Domain.Risk;

namespace PMPlatform.Tests.Unit.Application.Risk;

/// <summary>
/// The risk state machine of TASK-055: Identified → Assessed → Treatment ⇄ Monitoring → Closed, an acceptance that returns the
/// risk for review, and a reopen that needs its own permission.
/// </summary>
public sealed class RiskWorkflowTests
{
    /// <summary>The states a rated risk can be in while open.</summary>
    private static readonly RiskStatus[] Rated = [RiskStatus.Assessed, RiskStatus.Treatment, RiskStatus.Monitoring];

    private static readonly RiskCommand[] NeedARating = [RiskCommand.StartTreatment, RiskCommand.Monitor, RiskCommand.Accept];

    [Fact]
    public void TheWorkflowHasExactlyTheTwelveEdgesOfTask055()
    {
        Assert.Equal(
            [
                (RiskStatus.Identified, RiskStatus.Assessed),
                (RiskStatus.Identified, RiskStatus.Closed),
                (RiskStatus.Assessed, RiskStatus.Treatment),
                (RiskStatus.Assessed, RiskStatus.Monitoring),
                (RiskStatus.Assessed, RiskStatus.Closed),
                (RiskStatus.Treatment, RiskStatus.Monitoring),
                (RiskStatus.Treatment, RiskStatus.Closed),
                (RiskStatus.Monitoring, RiskStatus.Assessed),
                (RiskStatus.Monitoring, RiskStatus.Treatment),
                (RiskStatus.Monitoring, RiskStatus.Closed),
                (RiskStatus.Closed, RiskStatus.Identified),
                (RiskStatus.Closed, RiskStatus.Assessed),
            ],
            RiskWorkflow.Transitions.OrderBy(t => t.From).ThenBy(t => t.To));
    }

    /// <summary>Every edge a command takes is an edge of the state machine, and each edge is taken by some command.</summary>
    [Fact]
    public void TheCommandsTakeExactlyTheEdgesOfTheStateMachine()
    {
        HashSet<(RiskStatus, RiskStatus)> taken = [];
        foreach (RiskCommand command in Enum.GetValues<RiskCommand>())
        {
            foreach (RiskStatus from in Enum.GetValues<RiskStatus>())
            {
                foreach (bool assessed in new[] { false, true })
                {
                    if (RiskWorkflow.TargetOf(command, from, assessed) is { } to && to != from)
                    {
                        Assert.True(RiskWorkflow.Allows(from, to), $"{command} takes {from} → {to}, which is not an edge.");
                        taken.Add((from, to));
                    }
                }
            }
        }

        Assert.Equal(RiskWorkflow.Transitions.OrderBy(t => t.From).ThenBy(t => t.To), taken.OrderBy(t => t.Item1).ThenBy(t => t.Item2));
    }

    /// <summary>The first assessment takes an identified risk to ASSESSED; a reassessment keeps the risk where it is.</summary>
    [Fact]
    public void AnAssessmentRatesARiskWithoutMovingAnAssessedOne()
    {
        Assert.Equal(RiskStatus.Assessed, RiskWorkflow.TargetOf(RiskCommand.Assess, RiskStatus.Identified, assessed: false));
        Assert.All(Rated, status => Assert.Equal(status, RiskWorkflow.TargetOf(RiskCommand.Assess, status, assessed: true)));
    }

    /// <summary>An identified risk is neither treated, monitored nor accepted before it is rated.</summary>
    [Fact]
    public void AnUnratedRiskIsAssessedFirst() =>
        Assert.All(NeedARating, command => Assert.Null(RiskWorkflow.TargetOf(command, RiskStatus.Identified, assessed: false)));

    /// <summary>The gate decision: an acceptance holds the risk in MONITORING, and its revocation or expiry returns it to ASSESSED for review.</summary>
    [Fact]
    public void AnAcceptanceThatEndsReturnsTheRiskForReview()
    {
        Assert.All(Rated, status => Assert.Equal(RiskStatus.Monitoring, RiskWorkflow.TargetOf(RiskCommand.Accept, status, assessed: true)));
        Assert.Equal(RiskStatus.Assessed, RiskWorkflow.TargetOf(RiskCommand.RevokeAcceptance, RiskStatus.Monitoring, assessed: true));
        Assert.Equal(RiskStatus.Assessed, RiskWorkflow.TargetOf(RiskCommand.ExpireAcceptance, RiskStatus.Monitoring, assessed: true));
    }

    /// <summary>CLOSED is left only by a reopen, back to review: ASSESSED once assessed, IDENTIFIED otherwise.</summary>
    [Fact]
    public void ClosedIsLeftOnlyByAReopen()
    {
        Assert.Equal(RiskStatus.Assessed, RiskWorkflow.TargetOf(RiskCommand.Reopen, RiskStatus.Closed, assessed: true));
        Assert.Equal(RiskStatus.Identified, RiskWorkflow.TargetOf(RiskCommand.Reopen, RiskStatus.Closed, assessed: false));
        Assert.All(
            Enum.GetValues<RiskCommand>().Where(c => c != RiskCommand.Reopen),
            command => Assert.Null(RiskWorkflow.TargetOf(command, RiskStatus.Closed, assessed: true)));
        Assert.All(
            Enum.GetValues<RiskStatus>().Where(s => s != RiskStatus.Closed),
            status => Assert.Null(RiskWorkflow.TargetOf(RiskCommand.Reopen, status, assessed: true)));
    }

    /// <summary>
    /// The acceptance criterion: reopening is decided on RISK_REOPEN, never on the general edit permission. Rating and acceptance
    /// are decided on their own permissions too (ADR-013: authority unchanged).
    /// </summary>
    [Fact]
    public void EachControlledCommandHasItsOwnPermission()
    {
        Assert.Equal(PermissionCatalogue.RiskReopen, RiskWorkflow.PermissionOf(RiskCommand.Reopen));
        Assert.Equal(PermissionCatalogue.RiskAssess, RiskWorkflow.PermissionOf(RiskCommand.Assess));
        Assert.Equal(PermissionCatalogue.RiskAccept, RiskWorkflow.PermissionOf(RiskCommand.Accept));
        Assert.Equal(PermissionCatalogue.RiskAccept, RiskWorkflow.PermissionOf(RiskCommand.RevokeAcceptance));
        Assert.Equal(
            [RiskCommand.Reopen],
            Enum.GetValues<RiskCommand>().Where(c => c != RiskCommand.ExpireAcceptance && RiskWorkflow.PermissionOf(c) == PermissionCatalogue.RiskReopen));
        Assert.Throws<ArgumentOutOfRangeException>(() => RiskWorkflow.PermissionOf(RiskCommand.ExpireAcceptance));
    }

    /// <summary>A reminder's condition compares the status as <c>risk.status</c> holds it.</summary>
    [Fact]
    public void TheConditionSourceAnswersTheStatusAsTheColumnHoldsIt() =>
        Assert.All(
            Enum.GetValues<RiskStatus>(),
            status => Assert.Equal(JsonNamingPolicy.SnakeCaseUpper.ConvertName(status.ToString()), RiskConditionSource.StatusText(status)));
}
