using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.ChangeRequest.EventHandlers;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Tests.Unit.Application.ChangeRequest;

/// <summary>
/// What an approval issues (TASK-060): one authorisation per governed commitment the approved revision changes, ISSUED, pinned to the
/// version its evaluation was against, keyed by the request, the revision and the scope — so issuing the same approval again yields the
/// same keys, which the unique index refuses a second time (api-conventions R-39).
/// </summary>
public sealed class ChangeAuthorizationIssuanceTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Baseline = Guid.NewGuid();
    private static readonly Guid Budget = Guid.NewGuid();
    private static readonly Guid Run = Guid.NewGuid();

    [Fact]
    public void EachChangedCommitmentIsIssuedOneAuthorizationPinnedToTheVersionEvaluated()
    {
        ChangeRequestEntity request = Request(schedule: 6, cost: 40000m);

        Assert.Equal(
            [
                (ChangeAuthorizationScope.Rebaseline, "Schedule", "ProjectBaseline", Baseline, 3, ChangeAuthorizationStatus.Issued, Run),
                (ChangeAuthorizationScope.CommitmentChange, "FinancialKpi", "FinancialCommitment", Budget, 2, ChangeAuthorizationStatus.Issued, Run),
            ],
            ChangeRequestApprovalOutcomeHandler.Issue(request, Evaluation(request), Outcome(request), Now)
                .Select(a => (a.AuthorizationScope, a.TargetModule, a.TargetType, a.TargetId, a.TargetRevisionNo, a.Status, a.ApprovalInstanceId)));
    }

    /// <summary>A change with neither a schedule nor a cost impact changes no commitment a module applies an authorisation to (F-4).</summary>
    [Fact]
    public void AChangeOfNoGovernedCommitmentIsIssuedNone()
    {
        ChangeRequestEntity request = Request(schedule: null, cost: null);
        Assert.Empty(ChangeRequestApprovalOutcomeHandler.Issue(request, Evaluation(request), Outcome(request), Now));
    }

    /// <summary>The key is derived, never fresh: the same approval issued again yields the same keys; another revision, others.</summary>
    [Fact]
    public void TheIssuanceKeyIsTheRequestTheRevisionAndTheScope()
    {
        ChangeRequestEntity request = Request(schedule: 6, cost: 40000m);
        string[] first = [.. ChangeRequestApprovalOutcomeHandler.Issue(request, Evaluation(request), Outcome(request), Now).Select(a => a.IdempotencyKey)];
        string[] again = [.. ChangeRequestApprovalOutcomeHandler.Issue(request, Evaluation(request), Outcome(request), Now.AddMinutes(1)).Select(a => a.IdempotencyKey)];

        Assert.Equal([$"ChangeRequest:{request.Id}:r2:REBASELINE", $"ChangeRequest:{request.Id}:r2:COMMITMENT_CHANGE"], first);
        Assert.Equal(first, again);
        request.RevisionNo = 3;
        Assert.DoesNotContain(ChangeRequestApprovalOutcomeHandler.Issue(request, Evaluation(request), Outcome(request), Now).Select(a => a.IdempotencyKey), first.Contains);
    }

    private static ChangeRequestEntity Request(int? schedule, decimal? cost) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        Title = new NarrativeText("Change", Language.En),
        Justification = new NarrativeText("Why", Language.En),
        Status = ChangeRequestStatus.Approved,
        RevisionNo = 2,
        ScheduleImpactDays = schedule,
        CostImpactSar = cost is { } amount ? new Money(amount) : null,
    };

    private static MaterialityEvaluation Evaluation(ChangeRequestEntity request) => new()
    {
        ChangeRequestId = request.Id,
        RevisionNo = request.RevisionNo,
        ProjectBaselineId = Baseline,
        ProjectBaselineVersionNo = 3,
        BaselineDurationDays = 60,
        FinancialCommitmentId = request.CostImpactSar is null ? null : Budget,
        FinancialCommitmentVersionNo = request.CostImpactSar is null ? null : 2,
        BaselineBudgetSar = request.CostImpactSar is null ? null : new Money(1000000m),
        ResultingBandNo = 1,
    };

    private static ApprovalOutcomeRecorded Outcome(ChangeRequestEntity request) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = ApprovalOutcomeRecorded.Type,
        SchemaVersion = ApprovalOutcomeRecorded.CurrentSchemaVersion,
        Kind = EventKind.DomainEvent,
        MessageKey = $"{ApprovalOutcomeRecorded.Type}:apr-{Run}-outcome",
        IdempotencyKey = $"apr-{Run}-outcome",
        OccurredAt = Now,
        SourceModule = "Approval",
        CorrelationId = Guid.NewGuid(),
        Actor = new EventActor(AuditActorType.User, Guid.NewGuid()),
        Subject = new EventSubject("ChangeRequest", "ChangeRequest", request.Id, request.RevisionNo),
        Scope = new EventScope(request.ProjectId, null, null),
        Data = new ApprovalOutcomeData(Run, "CHANGE_REQUEST", ApprovalOutcomeDecision.Approved, Now, Guid.NewGuid(), Guid.NewGuid()),
    };
}
