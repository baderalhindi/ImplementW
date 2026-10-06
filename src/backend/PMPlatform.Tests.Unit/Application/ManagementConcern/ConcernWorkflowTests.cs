using PMPlatform.Application.Features.ManagementConcern;
using PMPlatform.Domain.ManagementConcern;

namespace PMPlatform.Tests.Unit.Application.ManagementConcern;

/// <summary>
/// The workbook row's lifecycle, Open → Assigned → In Progress → Pending Validation → Resolved → Closed: every command takes a step of
/// it or keeps the state, WF-11's outcome takes the other two, and nothing else moves a concern.
/// </summary>
public sealed class ConcernWorkflowTests
{
    /// <summary>The two moves WF-11's outcome makes, not a command: validated, or returned for the next revision.</summary>
    private static readonly (ConcernStatus, ConcernStatus)[] ValidationOutcomes =
        [(ConcernStatus.PendingValidation, ConcernStatus.Resolved), (ConcernStatus.PendingValidation, ConcernStatus.InProgress)];

    [Fact]
    public void TheStateMachineIsTheRowsLifecycleWithAReturnFromValidation() =>
        Assert.Equal(
            [
                (ConcernStatus.Open, ConcernStatus.Assigned), (ConcernStatus.Assigned, ConcernStatus.InProgress),
                (ConcernStatus.InProgress, ConcernStatus.PendingValidation), (ConcernStatus.PendingValidation, ConcernStatus.Resolved),
                (ConcernStatus.PendingValidation, ConcernStatus.InProgress), (ConcernStatus.Resolved, ConcernStatus.Closed),
            ],
            ConcernWorkflow.Transitions.OrderBy(t => t.From).ThenBy(t => t.To == ConcernStatus.InProgress));

    /// <summary>Every command either keeps the state or takes a step of the state machine; the commands and the outcome reach every step.</summary>
    [Fact]
    public void TheCommandsAndTheValidationOutcomeTakeExactlyTheStateMachinesSteps()
    {
        HashSet<(ConcernStatus, ConcernStatus)> taken = [.. ValidationOutcomes];
        foreach (ConcernCommand command in Enum.GetValues<ConcernCommand>())
        {
            foreach (ConcernStatus from in Enum.GetValues<ConcernStatus>())
            {
                if (ConcernWorkflow.TargetOf(command, from) is { } to && to != from)
                {
                    Assert.Contains((from, to), ConcernWorkflow.Transitions);
                    taken.Add((from, to));
                }
            }
        }

        Assert.True(taken.SetEquals(ConcernWorkflow.Transitions));
    }

    [Fact]
    public void EachCommandTakesItsEdge()
    {
        Assert.Equal(ConcernStatus.Assigned, ConcernWorkflow.TargetOf(ConcernCommand.Assign, ConcernStatus.Open));
        Assert.Equal(ConcernStatus.InProgress, ConcernWorkflow.TargetOf(ConcernCommand.Assign, ConcernStatus.InProgress));
        Assert.Equal(ConcernStatus.InProgress, ConcernWorkflow.TargetOf(ConcernCommand.Start, ConcernStatus.Assigned));
        Assert.Equal(ConcernStatus.PendingValidation, ConcernWorkflow.TargetOf(ConcernCommand.SubmitResolution, ConcernStatus.InProgress));
        Assert.Equal(ConcernStatus.PendingValidation, ConcernWorkflow.TargetOf(ConcernCommand.Review, ConcernStatus.PendingValidation));
        Assert.Equal(ConcernStatus.Closed, ConcernWorkflow.TargetOf(ConcernCommand.Close, ConcernStatus.Resolved));
    }

    /// <summary>A concern is reassessed or reassigned only until its resolution goes to validation; a CLOSED one takes no command.</summary>
    [Fact]
    public void ACommandWithoutAnEdgeIsRefusedForItsReason()
    {
        (ConcernCommand Command, ConcernStatus From, bool Closed, bool NotEditable)[] refusals =
        [
            (ConcernCommand.Assess, ConcernStatus.PendingValidation, false, true),
            (ConcernCommand.Assign, ConcernStatus.Resolved, false, true),
            (ConcernCommand.Review, ConcernStatus.Closed, true, false),
            (ConcernCommand.Start, ConcernStatus.Open, false, false),
            (ConcernCommand.Close, ConcernStatus.InProgress, false, false),
        ];

        Assert.All(refusals, r =>
        {
            Assert.Null(ConcernWorkflow.TargetOf(r.Command, r.From));
            Assert.Equal((r.Closed, r.NotEditable), ConcernWorkflow.RefusalOf(r.Command, r.From));
        });
    }

    [Fact]
    public void ClosedIsFinal() =>
        Assert.All(Enum.GetValues<ConcernCommand>(), command => Assert.Null(ConcernWorkflow.TargetOf(command, ConcernStatus.Closed)));
}
