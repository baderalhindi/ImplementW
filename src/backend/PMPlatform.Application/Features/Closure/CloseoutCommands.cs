using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.Closure.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// A completion or closure case's moves along <see cref="CloseoutWorkflow"/>, and its readiness (TASK-063). The requester evaluates,
/// submits and withdraws; review, waiver and activation are AHDA's — internal users only (ADR-013). Every command finds its edge or is
/// refused, reads the project's state again (BR-CLO-034), applies its own rules, and saves the case with what it wrote in one transaction.
/// A submission freezes the readiness it was evaluated with (CLO-CC-07, BR-CLO-031); the review starts the WF-11 run (edge 27) that
/// decides the case; activation is <see cref="CloseoutActivation"/>.
/// </summary>
internal sealed class CloseoutCommands(
    ICloseoutRepository repository, CloseoutGate gate, CloseoutReadiness readiness, CloseoutActivation activation, IApprovalRequests approvals, IAuditTrail audit,
    TimeProvider timeProvider)
{
    /// <summary>Runs <paramref name="command"/> on the case; the case as saved, or the refusal with nothing saved.</summary>
    public async Task<AdministrationResult<TCase>> RunAsync<TCase>(
        Guid callerId, Guid caseId, CloseoutCommand command, uint? expectedVersion, CancellationToken cancellationToken, ReadinessWaiver? waiver = null)
        where TCase : CloseoutCase
    {
        bool internalOnly = command is CloseoutCommand.StartReview or CloseoutCommand.WaiveCheck or CloseoutCommand.Activate;
        Loaded<TCase> loaded = await gate.LoadCaseAsync<TCase>(callerId, PermissionOf(command), caseId, expectedVersion, internalOnly, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, TCase @case) = (loaded.Project!, loaded.Record!);
        if (CloseoutWorkflow.TargetOf(command, @case.Status) is not { } to)
        {
            return CloseoutWorkflow.IsFinal(@case.Status) ? AdministrationError.TerminalState
                : command == CloseoutCommand.Withdraw && @case.Status == CloseoutCaseStatus.UnderReview ? AdministrationError.Conflict(ClosureErrorCodes.CaseUnderReview)
                : command is CloseoutCommand.EvaluateReadiness or CloseoutCommand.WaiveCheck ? AdministrationError.Conflict(ClosureErrorCodes.CaseNotEditable)
                : AdministrationError.InvalidTransition;
        }

        if (command != CloseoutCommand.Withdraw && CloseoutEligibility.ProjectRefused(@case, project) is { } notEligible)
        {
            return notEligible;
        }

        await using ICloseoutWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (command == CloseoutCommand.Activate)
        {
            AdministrationError? activationRefused = await activation.ActivateAsync(work, @case, project, callerId, AuditActorType.User, now, cancellationToken).ConfigureAwait(false);
            return activationRefused is null ? @case : (AdministrationResult<TCase>)activationRefused;
        }

        CloseoutCaseStatus from = @case.Status;
        CloseoutStep step = command switch
        {
            CloseoutCommand.EvaluateReadiness => await EvaluateAsync(callerId, project, @case, now, cancellationToken).ConfigureAwait(false),
            CloseoutCommand.WaiveCheck => await WaiveAsync(callerId, project, @case, waiver ?? throw new ArgumentNullException(nameof(waiver)), now, cancellationToken)
                .ConfigureAwait(false),
            CloseoutCommand.Submit => await SubmitAsync(callerId, project, @case, from, now, cancellationToken).ConfigureAwait(false),
            CloseoutCommand.Withdraw => new CloseoutStep(null, () => CloseoutAudit.Transition(ClosureAuditEvents.CaseWithdrawn, callerId, project, from, @case)),
            CloseoutCommand.StartReview => await StartReviewAsync(callerId, project, @case, from, cancellationToken).ConfigureAwait(false),
            CloseoutCommand.Activate or _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a command of this step."),
        };
        if (step.Refusal is { } broken)
        {
            return broken;
        }

        @case.Status = to;
        CloseoutGate.Touch(@case, callerId, now);
        audit.Stage(step.Entry!());
        CloseoutSaveOutcome saved = await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false);
        return saved == CloseoutSaveOutcome.Saved ? @case : CloseoutGate.RefusalOf(saved);
    }

    /// <summary>Records a fresh evaluation of the case's criteria, with its roll-up.</summary>
    private async Task<CloseoutStep> EvaluateAsync(Guid callerId, ProjectFacts project, CloseoutCase @case, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReadinessCheck> evaluation = await readiness.EvaluateAsync(@case, callerId, now, cancellationToken).ConfigureAwait(false);
        ReadinessDetail evaluated = ReadinessRollUp.Of([.. await repository.ListReadinessAsync([@case.Id], cancellationToken).ConfigureAwait(false), .. evaluation]);
        AddAll(evaluation);
        return new CloseoutStep(null, () => CloseoutAudit.ReadinessEvaluated(callerId, project, @case, evaluated));
    }

    /// <summary>Records the waiver of a criterion that failed on the latest evaluation and may be waived.</summary>
    private async Task<CloseoutStep> WaiveAsync(Guid callerId, ProjectFacts project, CloseoutCase @case, ReadinessWaiver waiver, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await WaiverOfAsync(@case, waiver, callerId, now, cancellationToken).ConfigureAwait(false) is not { } waived)
        {
            return new CloseoutStep(
                ReadinessPolicy.IsWaivable(waiver.CheckCode)
                    ? AdministrationError.Rule(ClosureErrorCodes.CheckNotFailed, new FieldIssue("checkCode", FieldIssue.NotAllowed))
                    : AdministrationError.Rule(ClosureErrorCodes.CheckNotWaivable, new FieldIssue("checkCode", FieldIssue.NotAllowed)),
                null);
        }

        repository.Add(waived);
        return new CloseoutStep(null, () => CloseoutAudit.CheckWaived(callerId, project, @case, waived));
    }

    /// <summary>
    /// What review needs, and the readiness evaluated afresh: the evaluation the submission makes is the revision's frozen readiness snapshot
    /// (CLO-CC-07), and NOT_READY is not submitted. A RETURNED case comes back as the next revision, which a new WF-11 run reviews (TASK-035 D-9;
    /// CLO-CC-08).
    /// </summary>
    private async Task<CloseoutStep> SubmitAsync(
        Guid callerId, ProjectFacts project, CloseoutCase @case, CloseoutCaseStatus from, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (CloseoutEligibility.SubmissionRefused(@case, project, DateOnly.FromDateTime(now.UtcDateTime)) is { } incomplete)
        {
            return new CloseoutStep(incomplete, null);
        }

        IReadOnlyList<ReadinessCheck> evaluation = await readiness.EvaluateAsync(@case, callerId, now, cancellationToken).ConfigureAwait(false);
        ReadinessDetail snapshot = ReadinessRollUp.Of([.. await repository.ListReadinessAsync([@case.Id], cancellationToken).ConfigureAwait(false), .. evaluation]);
        if (CloseoutEligibility.BlockerOf(snapshot) is { } blocked)
        {
            return new CloseoutStep(blocked, null);
        }

        AddAll(evaluation);
        @case.RevisionNo += from == CloseoutCaseStatus.Returned ? 1 : 0;
        @case.SubmittedAt = now;
        return new CloseoutStep(null, () => CloseoutAudit.Transition(ClosureAuditEvents.CaseSubmitted, callerId, project, from, @case, snapshot));
    }

    /// <summary>
    /// Starts the revision's WF-11 run, staged, not saved: it commits with UNDER_REVIEW, or not at all. Its requester is the originator, so
    /// WF-11 keeps them from approving their own case (CLO-CC-13), gives an external originator no authority (ADR-013), and lets them withdraw it.
    /// </summary>
    private async Task<CloseoutStep> StartReviewAsync(Guid callerId, ProjectFacts project, CloseoutCase @case, CloseoutCaseStatus from, CancellationToken cancellationToken)
    {
        AuditSubject subject = CloseoutAudit.SubjectOf(@case);
        AdministrationResult<ApprovalInstanceDetail> run = await approvals.StartAsync(
            new ApprovalStart(
                new ApprovalSubject(subject.Module, subject.Type, @case.Id, @case.RevisionNo),
                @case is CompletionCase ? ClosureApprovalRouting.CompletionRoutingKey : ClosureApprovalRouting.ClosureRoutingKey,
                @case.RequestedByUserId,
                project.Id,
                project.DepartmentId,
                project.GovernanceProfileItemId,
                null,
                null),
            cancellationToken).ConfigureAwait(false);
        if (!run.Succeeded)
        {
            return new CloseoutStep(run.Error, null);
        }

        Guid runId = run.Value.Id;
        return new CloseoutStep(null, () => CloseoutAudit.Transition(ClosureAuditEvents.ReviewStarted, callerId, project, from, @case, approvalInstanceId: runId));
    }

    private void AddAll(IEnumerable<ReadinessCheck> rows)
    {
        foreach (ReadinessCheck row in rows)
        {
            repository.Add(row);
        }
    }

    /// <summary>
    /// The waiver row for a waivable criterion that failed on the case's latest evaluation, carrying how many records failed it; null when
    /// the criterion is not waivable or did not fail.
    /// </summary>
    private async Task<ReadinessCheck?> WaiverOfAsync(CloseoutCase @case, ReadinessWaiver waiver, Guid callerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!ReadinessPolicy.IsWaivable(waiver.CheckCode))
        {
            return null;
        }

        List<ReadinessCheck> evaluations = [.. (await repository.ListReadinessAsync([@case.Id], cancellationToken).ConfigureAwait(false)).Where(r => r.Result != ReadinessResult.Waived)];
        DateTimeOffset? latest = evaluations.Count == 0 ? null : evaluations.Max(r => r.EvaluatedAt);
        ReadinessCheck? failed = evaluations.SingleOrDefault(r => r.EvaluatedAt == latest && r.CheckCode == waiver.CheckCode && r.Result == ReadinessResult.Fail);
        return failed is null
            ? null
            : new ReadinessCheck
            {
                Id = Guid.CreateVersion7(now),
                CompletionCaseId = failed.CompletionCaseId,
                ClosureCaseId = failed.ClosureCaseId,
                CheckCode = waiver.CheckCode,
                Result = ReadinessResult.Waived,
                EvaluatedAt = now,
                BlockingCount = failed.BlockingCount,
                Detail = waiver.Reason,
                WaivedByUserId = callerId,
                CreatedAt = now,
                CreatedBy = callerId,
                UpdatedAt = now,
                UpdatedBy = callerId,
            };
    }

    private static string PermissionOf(CloseoutCommand command) => command switch
    {
        CloseoutCommand.EvaluateReadiness or CloseoutCommand.Submit or CloseoutCommand.Withdraw => PermissionCatalogue.CloseoutRaise,
        CloseoutCommand.WaiveCheck => PermissionCatalogue.CloseoutWaive,
        CloseoutCommand.StartReview => PermissionCatalogue.CloseoutReview,
        CloseoutCommand.Activate => PermissionCatalogue.CloseoutActivate,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
    };
}

/// <summary>A command's own rules, applied: its refusal, or the audit event to stage once the new status is set.</summary>
internal sealed record CloseoutStep(AdministrationError? Refusal, Func<AuditEntry>? Entry);
