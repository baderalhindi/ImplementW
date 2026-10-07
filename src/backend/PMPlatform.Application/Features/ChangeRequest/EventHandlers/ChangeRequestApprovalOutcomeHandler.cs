using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.ChangeRequest.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest.EventHandlers;

/// <summary>
/// Applies the outcome of a change request's WF-11 run (ADR-003 §8.2 edges 22, 28). APPROVED makes the request APPROVED and issues an
/// authorisation for each governed commitment its evaluated revision changes — a REBASELINE of the Approved Baseline it was evaluated
/// against, a COMMITMENT_CHANGE of the Approved Budget — each ISSUED and pinned to the version evaluated. It calls no target module:
/// approval changes no schedule and no budget; each authorisation takes effect only when its module applies it. RETURNED, REJECTED and
/// WITHDRAWN end the run as WF-11 decided it. It runs inside the outbox dispatch transaction, so it commits exactly when the delivery
/// mark does.
/// </summary>
/// <remarks>
/// Idempotent on its own as well (EV-4, EV-5): an outcome applies only to the revision under review while the request is UNDER_REVIEW,
/// and applying it leaves that state, so the same outcome again, or one for an older revision, is audited as ignored. Each authorisation
/// carries an issuance key derived from the request, its revision and its scope under a unique constraint, so no redelivery issues one twice.
/// </remarks>
internal sealed class ChangeRequestApprovalOutcomeHandler(
    IChangeRequestRepository repository, IProjectFactsReader projects, IAuditTrail audit, TimeProvider timeProvider) : IApprovalOutcomeHandler
{
    public string SubjectModule => ChangeRequestApprovalRouting.SubjectModule;

    public async Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        await using IChangeRequestWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        ChangeRequestEntity request = await repository.FindAsync(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false)
                                      ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no change request.");
        ProjectFacts project = await projects.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Change request {request.Id} names no project.");

        ChangeRequestStatus from = request.Status;
        if (from != ChangeRequestStatus.UnderReview || request.RevisionNo != outcome.Subject.RevisionNo)
        {
            audit.Stage(ChangeRequestAudit.OutcomeIgnored(project, request, outcome));
            await SaveAsync(outcome, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            request.Status = ChangeRequestWorkflow.OutcomeOf(outcome.Data.Decision);
            ChangeRequestGate.Touch(request, outcome.Data.DecidedByUserId, now);
            audit.Stage(ChangeRequestAudit.Decided(EventOf(request.Status), project, from, request, outcome));
            await SaveAsync(outcome, cancellationToken).ConfigureAwait(false);

            // Issued once the request is APPROVED, which is when the database accepts an authorisation.
            if (request.Status == ChangeRequestStatus.Approved)
            {
                MaterialityEvaluation evaluation = (await repository.ListLatestEvaluationsAsync([request.Id], cancellationToken).ConfigureAwait(false))
                                                   .SingleOrDefault(e => e.RevisionNo == request.RevisionNo)
                                                   ?? throw new InvalidOperationException($"Change request {request.Id} was approved without an evaluation of revision {request.RevisionNo}.");
                foreach (ChangeAuthorization authorization in Issue(request, evaluation, outcome, now))
                {
                    repository.Add(authorization);
                    audit.Stage(ChangeRequestAudit.Authorization(ChangeRequestAuditEvents.ChangeAuthorizationIssued, outcome.Data.DecidedByUserId, project, request, authorization));
                }

                await SaveAsync(outcome, cancellationToken).ConfigureAwait(false);
            }
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// One authorisation per governed commitment the approved revision changes, pinned to the version its evaluation was against. A
    /// scope or governance-profile change issues none: no module applies one yet (change-request.md F-4).
    /// </summary>
    internal static IEnumerable<ChangeAuthorization> Issue(ChangeRequestEntity request, MaterialityEvaluation evaluation, ApprovalOutcomeRecorded outcome, DateTimeOffset now)
    {
        if (request.ScheduleImpactDays is not null && evaluation is { ProjectBaselineId: { } baselineId, ProjectBaselineVersionNo: { } baselineVersion })
        {
            yield return New(request, outcome, now, ChangeAuthorizationScope.Rebaseline, ChangeTargets.ScheduleModule, ChangeTargets.ProjectBaseline, baselineId, baselineVersion);
        }

        if (request.CostImpactSar is not null && evaluation is { FinancialCommitmentId: { } commitmentId, FinancialCommitmentVersionNo: { } commitmentVersion })
        {
            yield return New(
                request, outcome, now, ChangeAuthorizationScope.CommitmentChange, ChangeTargets.FinancialKpiModule, ChangeTargets.FinancialCommitment, commitmentId, commitmentVersion);
        }
    }

    private static ChangeAuthorization New(
        ChangeRequestEntity request, ApprovalOutcomeRecorded outcome, DateTimeOffset now, ChangeAuthorizationScope scope, string module, string type, Guid targetId, int targetVersion) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            ChangeRequestId = request.Id,
            ApprovalInstanceId = outcome.Data.ApprovalInstanceId,
            AuthorizationScope = scope,
            TargetModule = module,
            TargetType = type,
            TargetId = targetId,
            TargetRevisionNo = targetVersion,
            IdempotencyKey = IssuanceKeyOf(request.Id, request.RevisionNo, scope),
            Status = ChangeAuthorizationStatus.Issued,
            IssuedAt = now,
            CreatedAt = now,
            CreatedBy = outcome.Data.DecidedByUserId,
            UpdatedAt = now,
            UpdatedBy = outcome.Data.DecidedByUserId,
        };

    /// <summary>An authorisation's issuance key (api-conventions R-39): the request, the revision approved and the scope.</summary>
    internal static string IssuanceKeyOf(Guid changeRequestId, int revisionNo, ChangeAuthorizationScope scope) =>
        $"ChangeRequest:{changeRequestId}:r{revisionNo}:{System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(scope.ToString())}";

    private static string EventOf(ChangeRequestStatus status) => status switch
    {
        ChangeRequestStatus.Approved => ChangeRequestAuditEvents.ChangeRequestApproved,
        ChangeRequestStatus.Returned => ChangeRequestAuditEvents.ChangeRequestReturned,
        ChangeRequestStatus.Rejected => ChangeRequestAuditEvents.ChangeRequestRejected,
        ChangeRequestStatus.Withdrawn => ChangeRequestAuditEvents.ChangeRequestWithdrawn,
        ChangeRequestStatus.Draft or ChangeRequestStatus.Submitted or ChangeRequestStatus.UnderReview or ChangeRequestStatus.Implementation
            or ChangeRequestStatus.Implemented or ChangeRequestStatus.Closed or _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Not an outcome of a review."),
    };

    /// <summary>Throwing rolls the dispatch back, and the outcome is delivered again later (TASK-035 D-8).</summary>
    private async Task SaveAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ChangeRequestSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved != ChangeRequestSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} could not be applied: {saved}.");
        }
    }
}
