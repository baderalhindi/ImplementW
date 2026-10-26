using PMPlatform.Application.Features.Closure;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Tests.Unit.Application.Closure;

/// <summary>
/// WF-10's readiness (§6) and eligibility (§4.3, §8): which criteria each case is evaluated against and which may be waived; the backend's
/// roll-up of a case's records; when a project admits a case; and what review needs.
/// </summary>
public sealed class ReadinessTests
{
    private static readonly DateOnly Today = new(2026, 10, 8);
    private static readonly DateTimeOffset Earlier = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Completion checks every source module and that open obligations are owned; normal closure what can still move once COMPLETED and the
    /// closure policy; the terminal path, never reconciled by a completion, everything completion checks and the closure policy.
    /// </summary>
    [Fact]
    public void EachCaseIsEvaluatedAgainstItsStagesCriteria()
    {
        ReadinessCheckCode[] sources =
        [
            ReadinessCheckCode.DecisionsSettled, ReadinessCheckCode.TasksDispositioned, ReadinessCheckCode.ScheduleReconciled, ReadinessCheckCode.MilestonesDispositioned,
            ReadinessCheckCode.RisksDispositioned, ReadinessCheckCode.IssuesDispositioned, ReadinessCheckCode.ChangesDispositioned,
            ReadinessCheckCode.SuspensionRequestsSettled, ReadinessCheckCode.ProgressReported, ReadinessCheckCode.FinancialsSettled,
        ];
        Assert.Equal([.. sources, ReadinessCheckCode.ObligationsOwned], ReadinessPolicy.ChecksOf(new CompletionCase()));
        Assert.Equal([.. sources, ReadinessCheckCode.ObligationsSatisfied], ReadinessPolicy.ChecksOf(new ClosureCase()));
        Assert.Equal(
            [
                ReadinessCheckCode.DecisionsSettled, ReadinessCheckCode.RisksDispositioned, ReadinessCheckCode.IssuesDispositioned, ReadinessCheckCode.ChangesDispositioned,
                ReadinessCheckCode.ObligationsSatisfied,
            ],
            ReadinessPolicy.ChecksOf(new ClosureCase { CompletionCaseId = Guid.NewGuid() }));
    }

    /// <summary>A decision still to land, an open suspension or resumption request and the obligations are settled where they live, never waived.</summary>
    [Fact]
    public void OnlyOpenItemDispositionsAreWaivable() =>
        Assert.Equal(
            [ReadinessCheckCode.DecisionsSettled, ReadinessCheckCode.SuspensionRequestsSettled, ReadinessCheckCode.ObligationsOwned, ReadinessCheckCode.ObligationsSatisfied],
            Enum.GetValues<ReadinessCheckCode>().Where(c => !ReadinessPolicy.IsWaivable(c)));

    [Fact]
    public void ACaseNeverEvaluatedIsIncomplete() =>
        Assert.Equal(new ReadinessDetail(ReadinessStatus.Incomplete, null, []), ReadinessRollUp.Of([]));

    /// <summary>
    /// The latest evaluation is the one that counts; a failed waivable criterion waived by the case is WAIVED, with its author and reason, and
    /// the case READY_WITH_CONDITIONS; a waiver of a criterion that may not be waived changes nothing; one that passes is READY.
    /// </summary>
    [Fact]
    public void TheRollUpReadsTheLatestEvaluationWithTheCasesWaivers()
    {
        Guid waivedBy = Guid.NewGuid();
        NarrativeText reason = new("Transferred to the operator.", Language.En);
        ReadinessCheck[] earlier = [Row(ReadinessCheckCode.TasksDispositioned, ReadinessResult.Pass, 0, Earlier), Row(ReadinessCheckCode.DecisionsSettled, ReadinessResult.Fail, 1, Earlier)];
        ReadinessCheck waiver = Row(ReadinessCheckCode.TasksDispositioned, ReadinessResult.Waived, 2, Earlier.AddHours(1));
        waiver.WaivedByUserId = waivedBy;
        waiver.Detail = reason;

        ReadinessDetail conditional = ReadinessRollUp.Of(
            [.. earlier, waiver, Row(ReadinessCheckCode.TasksDispositioned, ReadinessResult.Fail, 2, Later), Row(ReadinessCheckCode.DecisionsSettled, ReadinessResult.Pass, 0, Later)]);
        Assert.Equal(ReadinessStatus.ReadyWithConditions, conditional.Status);
        Assert.Equal(Later, conditional.EvaluatedAt);
        Assert.Equal(
            [
                new ReadinessCheckDetail(ReadinessCheckCode.DecisionsSettled, ReadinessResult.Pass, 0, false, null, null),
                new ReadinessCheckDetail(ReadinessCheckCode.TasksDispositioned, ReadinessResult.Waived, 2, true, waivedBy, reason),
            ],
            conditional.Checks);

        ReadinessCheck hardWaiver = Row(ReadinessCheckCode.DecisionsSettled, ReadinessResult.Waived, 1, Later);
        hardWaiver.WaivedByUserId = waivedBy;
        hardWaiver.Detail = reason;
        ReadinessDetail blocked = ReadinessRollUp.Of([hardWaiver, Row(ReadinessCheckCode.DecisionsSettled, ReadinessResult.Fail, 1, Later.AddHours(1))]);
        Assert.Equal((ReadinessStatus.NotReady, ReadinessResult.Fail), (blocked.Status, Assert.Single(blocked.Checks).Result));

        Assert.Equal(ReadinessStatus.Ready, ReadinessRollUp.Of([waiver, Row(ReadinessCheckCode.TasksDispositioned, ReadinessResult.Pass, 0, Later)]).Status);
    }

    [Fact]
    public void ABlockerNamesEachFailingCriterion()
    {
        ReadinessDetail readiness = ReadinessRollUp.Of(
        [
            Row(ReadinessCheckCode.TasksDispositioned, ReadinessResult.Fail, 3, Later), Row(ReadinessCheckCode.RisksDispositioned, ReadinessResult.Pass, 0, Later),
            Row(ReadinessCheckCode.ObligationsOwned, ReadinessResult.Fail, 1, Later),
        ]);

        AdministrationError blocked = CloseoutEligibility.BlockerOf(readiness)!;
        Assert.Equal((AdministrationErrorKind.RuleViolated, ClosureErrorCodes.BlockerExists), (blocked.Kind, blocked.Code));
        Assert.Equal([new FieldIssue("TASKS_DISPOSITIONED", FieldIssue.NotAllowed), new FieldIssue("OBLIGATIONS_OWNED", FieldIssue.NotAllowed)], blocked.Fields);
        Assert.Null(CloseoutEligibility.BlockerOf(ReadinessRollUp.Of([Row(ReadinessCheckCode.TasksDispositioned, ReadinessResult.Pass, 0, Later)])));
    }

    /// <summary>
    /// BR-CLO-002, BR-CLO-003: a completion case is of an ACTIVE project; a closure case of a COMPLETED project on the normal path and of a
    /// SUSPENDED one on the terminal path; no other state admits either.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryState))]
    public void AProjectAdmitsACaseOnlyInItsStagesState(ProjectLifecycleState state)
    {
        ProjectFacts project = Project(state);
        Assert.Equal(state == ProjectLifecycleState.Active, CloseoutEligibility.ProjectRefused(new CompletionCase(), project) is null);
        Assert.Equal(state == ProjectLifecycleState.Completed, CloseoutEligibility.ProjectRefused(new ClosureCase { CompletionCaseId = Guid.NewGuid() }, project) is null);
        Assert.Equal(state == ProjectLifecycleState.Suspended, CloseoutEligibility.ProjectRefused(new ClosureCase(), project) is null);
        Assert.Equal(ClosureErrorCodes.ProjectNotEligible, CloseoutEligibility.ProjectRefused(new CompletionCase(), Project(ProjectLifecycleState.Closed))!.Code);
    }

    public static TheoryData<ProjectLifecycleState> EveryState() => [.. Enum.GetValues<ProjectLifecycleState>()];

    /// <summary>The actual completion date is never after today, nor before the project's activation; without one a completion is not reviewed.</summary>
    [Theory]
    [InlineData(0, null)]
    [InlineData(-10, null)]
    [InlineData(1, FieldIssue.NotAllowed)]
    [InlineData(-31, FieldIssue.BeforeStart)]
    public void TheActualCompletionDateFallsBetweenActivationAndToday(int days, string? issue)
    {
        ProjectFacts project = Project(ProjectLifecycleState.Active) with { ActivatedAt = new DateTimeOffset(Today.AddDays(-30).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) };
        AdministrationError? refused = CloseoutEligibility.CompletionFieldsRefused(new CompletionCaseChanges(Today.AddDays(days), null), project, Today);
        Assert.Equal(issue, refused?.Fields.Single().Code);
        Assert.Equal(issue is null ? null : ClosureErrorCodes.CompletionDateInvalid, refused?.Code);
    }

    [Fact]
    public void ReviewNeedsTheCompletionDateAndNarrativeAndTheClosureNarrative()
    {
        ProjectFacts project = Project(ProjectLifecycleState.Active);
        NarrativeText narrative = new("Delivered.", Language.En);
        Assert.Equal([new FieldIssue("actualProjectCompletionDate", FieldIssue.Required)], CloseoutEligibility.SubmissionRefused(new CompletionCase { CompletionNarrative = narrative }, project, Today)!.Fields);
        Assert.Equal([new FieldIssue("completionNarrative", FieldIssue.Required)], CloseoutEligibility.SubmissionRefused(new CompletionCase { ActualProjectCompletionDate = Today }, project, Today)!.Fields);
        Assert.Null(CloseoutEligibility.SubmissionRefused(new CompletionCase { ActualProjectCompletionDate = Today, CompletionNarrative = narrative }, project, Today));
        Assert.Equal([new FieldIssue("closureNarrative", FieldIssue.Required)], CloseoutEligibility.SubmissionRefused(new ClosureCase(), project, Today)!.Fields);
        Assert.Null(CloseoutEligibility.SubmissionRefused(new ClosureCase { ClosureNarrative = narrative }, project, Today));
    }

    /// <summary>
    /// Acceptance criterion 2 at the source of every module's refusal: a write permission on a CLOSED project is 409 PROJECT_CLOSED; a read, or
    /// a write on a project in any other state, is not refused by it.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryState))]
    public void AClosedProjectRefusesEveryWritePermissionAndNoRead(ProjectLifecycleState state)
    {
        foreach (PMPlatform.Application.Common.Authorization.PermissionDefinition permission in PMPlatform.Application.Common.Authorization.PermissionCatalogue.Platform.Definitions)
        {
            AdministrationError? refused = ClosedProjectGuard.Refusal(Project(state), permission.Code);
            bool refuses = state == ProjectLifecycleState.Closed && permission.Mode == PMPlatform.Application.Common.Authorization.AccessMode.Write;
            Assert.Equal(refuses ? AdministrationErrorKind.Conflict : null, refused?.Kind);
            Assert.Equal(refuses ? ProjectErrorCodes.Closed : null, refused?.Code);
        }
    }

    private static ProjectFacts Project(ProjectLifecycleState state) =>
        new(Guid.NewGuid(), "PRJ-000001", Guid.NewGuid(), null, Guid.NewGuid(), state, Guid.NewGuid(), null, new DateTimeOffset(Today.AddDays(-30).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), ParticipationMode.AhdaManaged, new NarrativeText("Project", Language.En));

    private static ReadinessCheck Row(ReadinessCheckCode code, ReadinessResult result, int blocking, DateTimeOffset at) =>
        new() { Id = Guid.NewGuid(), CompletionCaseId = Guid.Empty, CheckCode = code, Result = result, BlockingCount = blocking, EvaluatedAt = at };
}
