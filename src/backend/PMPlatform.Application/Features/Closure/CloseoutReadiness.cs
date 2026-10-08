using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// Evaluates a case's criteria (<see cref="ReadinessPolicy.ChecksOf"/>) against the source modules' own read contracts (ADR-003 §8.2
/// edges 14, 27, 39–46): each module answers for its own records and WF-10 counts what keeps a criterion from passing (WF-10 P4,
/// CLO-CC-15). Nothing is changed anywhere: no task completed, milestone achieved, risk accepted or change closed (P6, CLO-CC-17). The
/// rows are built, not added: the caller adds them only when its command goes ahead, so a refused command leaves nothing behind.
/// </summary>
internal sealed class CloseoutReadiness(
    ICloseoutRepository repository,
    IApprovalSettlementReader approvals,
    IProjectTaskCloseoutReader tasks,
    IScheduleCloseoutReader schedules,
    IMilestoneCloseoutReader milestones,
    IRiskCloseoutReader risks,
    IConcernCloseoutReader concerns,
    IChangeRequestCloseoutReader changes,
    ISuspensionCloseoutReader suspensions,
    IProgressCloseoutReader progress,
    IFinancialKpiCloseoutReader financials)
{
    /// <summary>One PASS or FAIL row per criterion of the case, all evaluated at <paramref name="now"/> by <paramref name="actorId"/>.</summary>
    public async Task<IReadOnlyList<ReadinessCheck>> EvaluateAsync(CloseoutCase @case, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@case);
        List<ReadinessCheck> rows = [];
        foreach (ReadinessCheckCode code in ReadinessPolicy.ChecksOf(@case))
        {
            int blocking = await BlockingCountAsync(code, @case, cancellationToken).ConfigureAwait(false);
            rows.Add(new ReadinessCheck
            {
                Id = Guid.CreateVersion7(now),
                CompletionCaseId = @case is CompletionCase ? @case.Id : null,
                ClosureCaseId = @case is ClosureCase ? @case.Id : null,
                CheckCode = code,
                Result = blocking == 0 ? ReadinessResult.Pass : ReadinessResult.Fail,
                EvaluatedAt = now,
                BlockingCount = blocking,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }

        return rows;
    }

    /// <summary>How many of the project's records keep <paramref name="code"/> from passing.</summary>
    private async Task<int> BlockingCountAsync(ReadinessCheckCode code, CloseoutCase @case, CancellationToken cancellationToken)
    {
        Guid projectId = @case.ProjectId;
        switch (code)
        {
            case ReadinessCheckCode.DecisionsSettled:
                return await approvals.CountUnsettledAsync(projectId, cancellationToken).ConfigureAwait(false);
            case ReadinessCheckCode.TasksDispositioned:
                return await tasks.CountOpenAsync(projectId, cancellationToken).ConfigureAwait(false);
            case ReadinessCheckCode.ScheduleReconciled:
                return (await schedules.ReadAsync(projectId, cancellationToken).ConfigureAwait(false)).OpenCandidates;
            case ReadinessCheckCode.MilestonesDispositioned:
                return (await schedules.ReadAsync(projectId, cancellationToken).ConfigureAwait(false)).PlannedMilestones
                       + await milestones.CountOpenClaimsAsync(projectId, cancellationToken).ConfigureAwait(false);
            case ReadinessCheckCode.RisksDispositioned:
                return await risks.CountOpenAsync(projectId, cancellationToken).ConfigureAwait(false);
            case ReadinessCheckCode.IssuesDispositioned:
                return await concerns.CountOpenAsync(projectId, cancellationToken).ConfigureAwait(false);
            case ReadinessCheckCode.ChangesDispositioned:
                // An implementation under way may be finished once the project is COMPLETED (WF-08 lets it be marked implemented and closed),
                // so completion waits only for undecided and unstarted requests; closure waits for every request to be final (BR-CLO-024).
                ChangeRequestCloseoutPosition position = await changes.ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
                return position.Undecided + position.ApprovedNotStarted + (@case is CompletionCase ? 0 : position.InImplementation);
            case ReadinessCheckCode.SuspensionRequestsSettled:
                return await suspensions.CountOpenAsync(projectId, cancellationToken).ConfigureAwait(false);
            case ReadinessCheckCode.ProgressReported:
                // Final progress is WF-02's published state, not 100% (WF-10 §8.1): one unpublished submission, or none published yet, fails it.
                ProgressCloseoutPosition reported = await progress.ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
                return reported.Unpublished + (reported.HasPublished ? 0 : 1);
            case ReadinessCheckCode.FinancialsSettled:
                FinancialKpiCloseoutPosition financial = await financials.ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
                return financial.Unpublished + financial.OpenVersions;
            case ReadinessCheckCode.ObligationsOwned:
                return (await repository.ListObligationsAsync(projectId, cancellationToken).ConfigureAwait(false))
                    .Count(o => PostProjectObligationRules.IsOpen(o.Status) && (o.OwnerUserId is null || o.DueDate is null));
            case ReadinessCheckCode.ObligationsSatisfied:
                return (await repository.ListObligationsAsync(projectId, cancellationToken).ConfigureAwait(false)).Count(o => PostProjectObligationRules.IsOpen(o.Status));
            default:
                throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown readiness criterion.");
        }
    }
}
