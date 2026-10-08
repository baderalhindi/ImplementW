using PMPlatform.Application.Features.Project;
using PMPlatform.Application.Features.Project.EventHandlers;
using PMPlatform.Domain.Project;

namespace PMPlatform.Tests.Unit.Application.Project;

/// <summary>
/// TASK-041's state machine with TASK-062's two edges and TASK-063's three: exactly the twelve, so that no project skips UNDER_REVIEW
/// (PTBC-002 is Open: no fast-track rule is authorised), ACTIVE is first reached only from APPROVED_PLANNED, by the activation command, a
/// project leaves ACTIVE only for SUSPENDED or COMPLETED, COMPLETED is reached only from ACTIVE, and CLOSED — from COMPLETED, or from
/// SUSPENDED on WF-10's terminal path — is terminal.
/// </summary>
public sealed class ProjectLifecycleTests
{
    public static TheoryData<ProjectLifecycleState, ProjectLifecycleState> EveryPair()
    {
        TheoryData<ProjectLifecycleState, ProjectLifecycleState> data = [];
        foreach (ProjectLifecycleState from in Enum.GetValues<ProjectLifecycleState>())
        {
            foreach (ProjectLifecycleState to in Enum.GetValues<ProjectLifecycleState>().Where(to => to != from))
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Fact]
    public void TheMachineHasExactlyTheSevenEdgesOfTask041TheTwoOfTask062AndTheThreeOfTask063()
    {
        Assert.Equal(
            [
                (ProjectLifecycleState.Draft, ProjectLifecycleState.Submitted),
                (ProjectLifecycleState.Submitted, ProjectLifecycleState.Draft),
                (ProjectLifecycleState.Submitted, ProjectLifecycleState.UnderReview),
                (ProjectLifecycleState.UnderReview, ProjectLifecycleState.Returned),
                (ProjectLifecycleState.UnderReview, ProjectLifecycleState.ApprovedPlanned),
                (ProjectLifecycleState.Returned, ProjectLifecycleState.Submitted),
                (ProjectLifecycleState.ApprovedPlanned, ProjectLifecycleState.Active),
                (ProjectLifecycleState.Active, ProjectLifecycleState.Suspended),
                (ProjectLifecycleState.Active, ProjectLifecycleState.Completed),
                (ProjectLifecycleState.Suspended, ProjectLifecycleState.Active),
                (ProjectLifecycleState.Suspended, ProjectLifecycleState.Closed),
                (ProjectLifecycleState.Completed, ProjectLifecycleState.Closed),
            ],
            ProjectLifecycle.Transitions.OrderBy(t => t.From).ThenBy(t => t.To));
    }

    /// <summary>Acceptance criterion 3: every path into APPROVED_PLANNED passes UNDER_REVIEW.</summary>
    [Fact]
    public void NoPathReachesApprovedPlannedWithoutUnderReview()
    {
        HashSet<ProjectLifecycleState> reachable = [ProjectLifecycleState.Draft];
        Queue<ProjectLifecycleState> frontier = new([ProjectLifecycleState.Draft]);
        while (frontier.TryDequeue(out ProjectLifecycleState state))
        {
            foreach ((_, ProjectLifecycleState next) in ProjectLifecycle.Transitions.Where(t => t.From == state && t.To != ProjectLifecycleState.UnderReview))
            {
                if (reachable.Add(next))
                {
                    frontier.Enqueue(next);
                }
            }
        }

        Assert.DoesNotContain(ProjectLifecycleState.ApprovedPlanned, reachable);
        Assert.DoesNotContain(ProjectLifecycleState.Active, reachable);
    }

    /// <summary>
    /// Acceptance criterion 2, with TASK-062 and TASK-063: ACTIVE is entered from APPROVED_PLANNED by the activation command or from
    /// SUSPENDED by a resumption, and left for SUSPENDED or — by an effected completion case — COMPLETED; SUSPENDED is left for ACTIVE or,
    /// on WF-10's terminal path, CLOSED.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPair))]
    public void ActiveIsEnteredFromApprovedPlannedOrSuspendedAndLeftOnlyForSuspendedOrCompleted(ProjectLifecycleState from, ProjectLifecycleState to)
    {
        if (to is ProjectLifecycleState.Active or ProjectLifecycleState.Suspended || from is ProjectLifecycleState.Active or ProjectLifecycleState.Suspended)
        {
            Assert.Equal(
                (from, to) is (ProjectLifecycleState.ApprovedPlanned, ProjectLifecycleState.Active) or (ProjectLifecycleState.Active, ProjectLifecycleState.Suspended)
                    or (ProjectLifecycleState.Suspended, ProjectLifecycleState.Active) or (ProjectLifecycleState.Active, ProjectLifecycleState.Completed)
                    or (ProjectLifecycleState.Suspended, ProjectLifecycleState.Closed),
                ProjectLifecycle.Allows(from, to));
        }
    }

    /// <summary>
    /// WF-10 BR-CLO-001 to BR-CLO-003, BR-CLO-020 (TASK-063): COMPLETED is entered only from ACTIVE and left only for CLOSED; CLOSED is entered
    /// from COMPLETED or SUSPENDED and left for nothing — it is terminal.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPair))]
    public void CompletedIsReachedOnlyFromActiveAndClosedIsTerminal(ProjectLifecycleState from, ProjectLifecycleState to)
    {
        if (to is ProjectLifecycleState.Completed or ProjectLifecycleState.Closed || from is ProjectLifecycleState.Completed or ProjectLifecycleState.Closed)
        {
            Assert.Equal(
                (from, to) is (ProjectLifecycleState.Active, ProjectLifecycleState.Completed) or (ProjectLifecycleState.Completed, ProjectLifecycleState.Closed)
                    or (ProjectLifecycleState.Suspended, ProjectLifecycleState.Closed),
                ProjectLifecycle.Allows(from, to));
        }
    }

    [Theory]
    [InlineData(ProjectLifecycleState.Draft, true)]
    [InlineData(ProjectLifecycleState.Returned, true)]
    [InlineData(ProjectLifecycleState.Submitted, false)]
    [InlineData(ProjectLifecycleState.UnderReview, false)]
    [InlineData(ProjectLifecycleState.ApprovedPlanned, false)]
    [InlineData(ProjectLifecycleState.Active, false)]
    [InlineData(ProjectLifecycleState.Closed, false)]
    public void OnlyADraftOrAReturnedProjectIsEdited(ProjectLifecycleState state, bool editable) =>
        Assert.Equal(editable, ProjectLifecycle.IsEditable(state));

    [Theory]
    [InlineData(1L, "PRJ-000001")]
    [InlineData(4211L, "PRJ-004211")]
    [InlineData(1234567L, "PRJ-1234567")]
    public void AFormalProjectIdIsItsSequenceNumberUnderOnePrefix(long number, string formalProjectId) =>
        Assert.Equal(formalProjectId, ProjectApprovalOutcomeHandler.FormalProjectIdOf(number));
}
