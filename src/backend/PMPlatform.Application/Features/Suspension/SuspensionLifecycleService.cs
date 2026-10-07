using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// A request's moves along <see cref="SuspensionWorkflow"/> (TASK-062). The requester submits and withdraws; review and activation are
/// AHDA's — internal users only (ADR-013). Every command finds its edge or is refused, reads the project's state again (WF-09 SUS-CC-03),
/// applies its own rules, and saves the request with what it wrote in one transaction. The review starts the WF-11 run (edge 23) that
/// decides the request; activation is <see cref="SuspensionActivation"/>.
/// </summary>
internal sealed class SuspensionLifecycleService(
    ISuspensionRepository repository, SuspensionGate gate, SuspensionActivation activation, IApprovalRequests approvals, IAuditTrail audit, TimeProvider timeProvider)
    : ISuspensionLifecycleService
{
    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> SubmitAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, suspensionRequestId, SuspensionCommand.Submit, expectedVersion, (step, _) =>
        {
            SuspensionRequest request = step.Request;
            if (SuspensionEligibility.SubmissionRefused(request, DateOnly.FromDateTime(step.Now.UtcDateTime)) is { } broken)
            {
                return Task.FromResult<AdministrationError?>(broken);
            }

            // A RETURNED request comes back as the next revision, which a new WF-11 run reviews (TASK-035 D-9).
            request.RevisionNo += step.From == SuspensionRequestStatus.Returned ? 1 : 0;
            request.SubmittedAt = step.Now;
            step.Stage(() => SuspensionAudit.Transition(SuspensionAuditEvents.RequestSubmitted, callerId, step.Project, step.From, request));
            return Task.FromResult<AdministrationError?>(null);
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> WithdrawAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, suspensionRequestId, SuspensionCommand.Withdraw, expectedVersion, (step, _) =>
        {
            step.Stage(() => SuspensionAudit.Transition(SuspensionAuditEvents.RequestWithdrawn, callerId, step.Project, step.From, step.Request));
            return Task.FromResult<AdministrationError?>(null);
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> StartReviewAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, suspensionRequestId, SuspensionCommand.StartReview, expectedVersion, async (step, ct) =>
        {
            // The run is staged, not saved: it commits with UNDER_REVIEW, or not at all (M-11). Its requester is the originator, so WF-11 keeps
            // them from approving their own request (SUS-CC-07), gives an external originator no authority (ADR-013), and lets them withdraw it.
            SuspensionRequest request = step.Request;
            AdministrationResult<ApprovalInstanceDetail> run = await approvals.StartAsync(
                new ApprovalStart(
                    new ApprovalSubject(SuspensionApprovalRouting.SubjectModule, SuspensionApprovalRouting.SubjectType, request.Id, request.RevisionNo),
                    SuspensionApprovalRouting.RoutingKeyOf(request.RequestType),
                    request.RequestedByUserId,
                    step.Project.Id,
                    step.Project.DepartmentId,
                    step.Project.GovernanceProfileItemId,
                    null,
                    null),
                ct).ConfigureAwait(false);
            if (!run.Succeeded)
            {
                return run.Error;
            }

            Guid runId = run.Value.Id;
            step.Stage(() => SuspensionAudit.Transition(SuspensionAuditEvents.ReviewStarted, callerId, step.Project, step.From, request, runId));
            return null;
        }, cancellationToken, internalOnly: true);

    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> ActivateAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, suspensionRequestId, SuspensionCommand.Activate, expectedVersion, (step, ct) =>
            activation.StageAsync(step.Request, step.Project, callerId, AuditActorType.User, step.Now, ct), cancellationToken, internalOnly: true);

    /// <summary>
    /// The shared path of every command: the permission and the person, the edge, the project's state, then the command's own rules under
    /// one unit of work, the new status, and the save. The command's audit event is staged only once its rules hold. Withdrawal is not held
    /// to the project's state: a request that can no longer take effect can still be taken back.
    /// </summary>
    private async Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> RunAsync(
        Guid callerId, Guid suspensionRequestId, SuspensionCommand command, uint? expectedVersion,
        Func<SuspensionStep, CancellationToken, Task<AdministrationError?>> apply, CancellationToken cancellationToken, bool internalOnly = false)
    {
        LoadedSuspensionRequest loaded = await gate.LoadAsync(callerId, PermissionOf(command), suspensionRequestId, expectedVersion, internalOnly, cancellationToken)
            .ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, SuspensionRequest request) = (loaded.Project!, loaded.Request!);
        if (SuspensionWorkflow.TargetOf(command, request.Status) is not { } to)
        {
            return SuspensionWorkflow.IsFinal(request.Status) ? AdministrationError.TerminalState
                : command == SuspensionCommand.Withdraw && request.Status == SuspensionRequestStatus.UnderReview ? AdministrationError.Conflict(SuspensionErrorCodes.UnderReview)
                : AdministrationError.InvalidTransition;
        }

        if (command != SuspensionCommand.Withdraw && SuspensionEligibility.ProjectRefused(request.RequestType, project) is { } notEligible)
        {
            return notEligible;
        }

        await using ISuspensionWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        SuspensionStep step = new(project, request, request.Status, timeProvider.GetUtcNow(), audit);
        if (await apply(step, cancellationToken).ConfigureAwait(false) is { } broken)
        {
            return broken;
        }

        request.Status = to;
        SuspensionGate.Touch(request, callerId, step.Now);
        step.Flush();
        return await gate.SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    private static string PermissionOf(SuspensionCommand command) => command switch
    {
        SuspensionCommand.Submit or SuspensionCommand.Withdraw => PermissionCatalogue.SuspensionRaise,
        SuspensionCommand.StartReview => PermissionCatalogue.SuspensionReview,
        SuspensionCommand.Activate => PermissionCatalogue.SuspensionActivate,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
    };
}

/// <summary>One command on one request, as its rules see it. Its audit event is built after the new status is set, so it records the move whole.</summary>
internal sealed class SuspensionStep(ProjectFacts project, SuspensionRequest request, SuspensionRequestStatus from, DateTimeOffset now, IAuditTrail audit)
{
    private Func<AuditEntry>? _entry;

    public ProjectFacts Project => project;

    public SuspensionRequest Request => request;

    public SuspensionRequestStatus From => from;

    public DateTimeOffset Now => now;

    public void Stage(Func<AuditEntry> entry) => _entry = entry;

    public void Flush()
    {
        if (_entry is not null)
        {
            audit.Stage(_entry());
        }
    }
}
