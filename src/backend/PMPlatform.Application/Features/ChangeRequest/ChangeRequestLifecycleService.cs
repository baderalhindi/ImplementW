using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.ChangeRequest.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// A change request's moves along <see cref="ChangeRequestWorkflow"/> (TASK-060). The requester submits and withdraws; review,
/// implementation and closure are AHDA's — internal users only (ADR-013). Every command finds its edge or is refused 409, applies its
/// own rules, and saves the request with what it wrote in one transaction. The review records the materiality evaluation and starts
/// the WF-11 run (edge 22) that decides the request.
/// </summary>
internal sealed class ChangeRequestLifecycleService(
    IChangeRequestRepository repository, ChangeRequestGate gate, ChangeRequestReferences references, MaterialityEvaluator materiality, IApprovalRequests approvals,
    IAuditTrail audit, TimeProvider timeProvider) : IChangeRequestLifecycleService
{
    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> SubmitAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, changeRequestId, ChangeRequestCommand.Submit, expectedVersion, async (step, ct) =>
        {
            ChangeRequestEntity request = step.Request;
            AdministrationError? broken = ChangeRequestReferences.IncompleteRefusal(request)
                                          ?? await references.ProfileRefusedAsync(step.Project, request.RequestedGovernanceProfileItemId, ct).ConfigureAwait(false)
                                          ?? await materiality.TargetsRefusedAsync(step.Project, request, ct).ConfigureAwait(false);
            if (broken is not null)
            {
                return broken;
            }

            // A RETURNED request comes back as the next revision, which a new WF-11 run reviews (TASK-035 D-9).
            request.RevisionNo += step.From == ChangeRequestStatus.Returned ? 1 : 0;
            request.SubmittedAt = step.Now;
            step.Stage(() => ChangeRequestAudit.Transition(ChangeRequestAuditEvents.ChangeRequestSubmitted, callerId, step.Project, step.From, request));
            return null;
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> WithdrawAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, changeRequestId, ChangeRequestCommand.Withdraw, expectedVersion, (step, _) =>
        {
            step.Stage(() => ChangeRequestAudit.Transition(ChangeRequestAuditEvents.ChangeRequestWithdrawn, callerId, step.Project, step.From, step.Request));
            return Task.FromResult<AdministrationError?>(null);
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> StartReviewAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, changeRequestId, ChangeRequestCommand.StartReview, expectedVersion, async (step, ct) =>
        {
            ChangeRequestEntity request = step.Request;
            AdministrationResult<MaterialityEvaluation> evaluated = await materiality.EvaluateAsync(step.Project, request, callerId, step.Now, ct).ConfigureAwait(false);
            if (!evaluated.Succeeded)
            {
                return evaluated.Error;
            }

            // The run is staged, not saved: it commits with UNDER_REVIEW, or not at all (M-11). Its requester is the originator, so WF-11
            // keeps them from approving their own change, gives an external originator no authority (ADR-013), and lets them withdraw it.
            MaterialityEvaluation evaluation = evaluated.Value;
            AdministrationResult<ApprovalInstanceDetail> run = await approvals.StartAsync(
                new ApprovalStart(
                    new ApprovalSubject(ChangeRequestApprovalRouting.SubjectModule, ChangeRequestApprovalRouting.SubjectType, request.Id, request.RevisionNo),
                    ChangeRequestApprovalRouting.RoutingKey,
                    request.RequestedByUserId,
                    step.Project.Id,
                    step.Project.DepartmentId,
                    step.Project.GovernanceProfileItemId,
                    evaluation.ResultingBandNo,
                    request.CostImpactSar is { } cost ? new Money(Math.Abs(cost.Amount)) : null),
                ct).ConfigureAwait(false);
            if (!run.Succeeded)
            {
                return run.Error;
            }

            // The evaluation is written while the request is still SUBMITTED, which is when the database accepts one: one per revision.
            repository.Add(evaluation);
            audit.Stage(ChangeRequestAudit.Evaluated(callerId, step.Project, request, evaluation));
            if (await repository.SaveAsync(ct).ConfigureAwait(false) != ChangeRequestSaveOutcome.Saved)
            {
                return AdministrationError.InvalidTransition;
            }

            Guid runId = run.Value.Id;
            step.Stage(() => ChangeRequestAudit.Transition(ChangeRequestAuditEvents.ReviewStarted, callerId, step.Project, step.From, request, runId, evaluation.ResultingBandNo));
            return null;
        }, cancellationToken, internalOnly: true);

    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> StartImplementationAsync(
        Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, changeRequestId, ChangeRequestCommand.StartImplementation, expectedVersion, (step, _) =>
        {
            step.Stage(() => ChangeRequestAudit.Transition(ChangeRequestAuditEvents.ImplementationStarted, callerId, step.Project, step.From, step.Request));
            return Task.FromResult<AdministrationError?>(null);
        }, cancellationToken, internalOnly: true);

    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> MarkImplementedAsync(
        Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, changeRequestId, ChangeRequestCommand.MarkImplemented, expectedVersion, async (step, ct) =>
        {
            // Implemented means every governed change took effect in its own module (WF-08 BR-CHG-025, BR-CHG-039): each authorisation applied.
            if ((await repository.ListAuthorizationsAsync([step.Request.Id], ct).ConfigureAwait(false)).Any(a => a.Status != ChangeAuthorizationStatus.Applied))
            {
                return AdministrationError.Conflict(ChangeRequestErrorCodes.AuthorizationPending);
            }

            step.Request.ImplementedAt = step.Now;
            step.Stage(() => ChangeRequestAudit.Transition(ChangeRequestAuditEvents.ChangeRequestImplemented, callerId, step.Project, step.From, step.Request));
            return null;
        }, cancellationToken, internalOnly: true);

    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> CloseAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, changeRequestId, ChangeRequestCommand.Close, expectedVersion, (step, _) =>
        {
            step.Request.ClosedAt = step.Now;
            step.Stage(() => ChangeRequestAudit.Transition(ChangeRequestAuditEvents.ChangeRequestClosed, callerId, step.Project, step.From, step.Request));
            return Task.FromResult<AdministrationError?>(null);
        }, cancellationToken, internalOnly: true);

    /// <summary>
    /// The shared path of every command: the permission and the person, the project's state, the edge, then the command's own rules under
    /// one unit of work, the new status, and the save. The command's audit event is staged only once its rules hold.
    /// </summary>
    private async Task<AdministrationResult<Versioned<ChangeRequestDetail>>> RunAsync(
        Guid callerId, Guid changeRequestId, ChangeRequestCommand command, uint? expectedVersion,
        Func<ChangeRequestStep, CancellationToken, Task<AdministrationError?>> apply, CancellationToken cancellationToken, bool internalOnly = false)
    {
        LoadedChangeRequest loaded = await gate.LoadAsync(callerId, PermissionOf(command), changeRequestId, expectedVersion, internalOnly, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ProjectFacts project, ChangeRequestEntity request) = (loaded.Project!, loaded.Request!);
        if (ChangeRequestWorkflow.TargetOf(command, request.Status) is not { } to)
        {
            return ChangeRequestWorkflow.IsFinal(request.Status) ? AdministrationError.TerminalState
                : command == ChangeRequestCommand.Withdraw && request.Status == ChangeRequestStatus.UnderReview ? AdministrationError.Conflict(ChangeRequestErrorCodes.UnderReview)
                : AdministrationError.InvalidTransition;
        }

        if (ProjectRefusal(command, project) is { } notEligible)
        {
            return notEligible;
        }

        await using IChangeRequestWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        ChangeRequestStep step = new(project, request, request.Status, timeProvider.GetUtcNow(), audit);
        if (await apply(step, cancellationToken).ConfigureAwait(false) is { } broken)
        {
            return broken;
        }

        request.Status = to;
        ChangeRequestGate.Touch(request, callerId, step.Now);
        step.Flush();
        return await gate.SaveRequestAsync(work, request, cancellationToken).ConfigureAwait(false);
    }

    private static string PermissionOf(ChangeRequestCommand command) => command switch
    {
        ChangeRequestCommand.Submit or ChangeRequestCommand.Withdraw => PermissionCatalogue.ChangeRequestRaise,
        ChangeRequestCommand.StartReview => PermissionCatalogue.ChangeRequestReview,
        ChangeRequestCommand.StartImplementation or ChangeRequestCommand.MarkImplemented or ChangeRequestCommand.Close => PermissionCatalogue.ChangeRequestImplement,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
    };

    private static AdministrationError? ProjectRefusal(ChangeRequestCommand command, ProjectFacts project) => command switch
    {
        ChangeRequestCommand.Submit or ChangeRequestCommand.Withdraw or ChangeRequestCommand.StartReview => ChangeRequestReferences.RaisingRefused(project),
        ChangeRequestCommand.StartImplementation => ChangeRequestReferences.ImplementationRefused(project),
        ChangeRequestCommand.MarkImplemented or ChangeRequestCommand.Close => ChangeRequestReferences.FinishingRefused(project),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command."),
    };
}

/// <summary>One command on one request, as its rules see it. Its audit event is built after the new status is set, so it records the move whole.</summary>
internal sealed class ChangeRequestStep(ProjectFacts project, ChangeRequestEntity request, ChangeRequestStatus from, DateTimeOffset now, IAuditTrail audit)
{
    private Func<AuditEntry>? _entry;

    public ProjectFacts Project => project;

    public ChangeRequestEntity Request => request;

    public ChangeRequestStatus From => from;

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
