using PMPlatform.Application.Features.Project;
using PMPlatform.Application.Features.Project.EventHandlers;
using PMPlatform.Domain.Project;

namespace PMPlatform.Tests.Unit.Application.Project;

/// <summary>
/// TASK-041's state machine: exactly the seven edges it builds, so that no project skips UNDER_REVIEW (PTBC-002 is Open:
/// no fast-track rule is authorised) and ACTIVE is reachable only from APPROVED_PLANNED, by the activation command.
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
    public void TheMachineHasExactlyTheSevenEdgesOfTask041()
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

    /// <summary>Acceptance criterion 2: ACTIVE has one way in, and TASK-041 builds no way out (TASK-062 and TASK-063 add theirs).</summary>
    [Theory]
    [MemberData(nameof(EveryPair))]
    public void ActiveIsEnteredOnlyFromApprovedPlannedAndLeftByNoEdgeYet(ProjectLifecycleState from, ProjectLifecycleState to)
    {
        if (to == ProjectLifecycleState.Active || from == ProjectLifecycleState.Active)
        {
            Assert.Equal(from == ProjectLifecycleState.ApprovedPlanned && to == ProjectLifecycleState.Active, ProjectLifecycle.Allows(from, to));
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
