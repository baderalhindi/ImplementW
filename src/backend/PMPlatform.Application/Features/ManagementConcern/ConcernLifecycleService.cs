using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.ManagementConcern.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ManagementConcern;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// A concern's moves along <see cref="ConcernWorkflow"/> (TASK-057). Every command is management — CONCERN_MANAGE, internal users only
/// (ADR-013) — finds its edge or is refused 409, applies its own rules, and saves the concern with what it wrote in one transaction.
/// The submitted resolution is validated through WF-11 (edge 24), whose run commits with PENDING_VALIDATION.
/// </summary>
internal sealed class ConcernLifecycleService(
    IManagementConcernRepository repository, ConcernGate gate, ConcernReferences references, ConcernSeverity severity, ConcernIntake intake,
    IApprovalRequests approvals, IAuditTrail audit, TimeProvider timeProvider) : IConcernLifecycleService
{
    public Task<AdministrationResult<Versioned<ConcernDetail>>> AssessAsync(
        Guid callerId, Guid concernId, IReadOnlyList<ConcernImpactInput> impacts, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(impacts);
        return RunAsync(callerId, concernId, ConcernCommand.Assess, expectedVersion, async (step, ct) =>
        {
            AdministrationResult<ComputedSeverity> computed = await severity.ComputeAsync(impacts, step.Now, ct).ConfigureAwait(false);
            if (!computed.Succeeded)
            {
                return computed.Error;
            }

            ConcernSeverityFields before = ConcernSeverityFields.Of(step.Concern);
            foreach (ConcernImpact earlier in await repository.FindImpactsAsync(concernId, ct).ConfigureAwait(false))
            {
                repository.Remove(earlier);
            }

            intake.Apply(step.Concern, computed.Value, impacts, callerId, step.Now);
            step.Stage(() => ConcernAudit.Assessed(callerId, step.Project, before, step.Concern));
            return null;
        }, cancellationToken);
    }

    public Task<AdministrationResult<Versioned<ConcernDetail>>> AssignAsync(
        Guid callerId, Guid concernId, Guid assigneeUserId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, concernId, ConcernCommand.Assign, expectedVersion, async (step, ct) =>
        {
            if (await references.AssigneeRefusedAsync(step.Project, assigneeUserId, ct).ConfigureAwait(false) is { } refused)
            {
                return refused;
            }

            step.Concern.AssigneeUserId = assigneeUserId;
            step.Stage(() => ConcernAudit.Transition(ConcernAuditEvents.ConcernAssigned, callerId, step.Project, step.From, step.Concern));
            return null;
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<ConcernDetail>>> StartAsync(Guid callerId, Guid concernId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, concernId, ConcernCommand.Start, expectedVersion, (step, _) =>
        {
            step.Stage(() => ConcernAudit.Transition(ConcernAuditEvents.WorkStarted, callerId, step.Project, step.From, step.Concern));
            return Task.FromResult<AdministrationError?>(null);
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<ConcernDetail>>> SubmitResolutionAsync(
        Guid callerId, Guid concernId, NarrativeText resolution, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return RunAsync(callerId, concernId, ConcernCommand.SubmitResolution, expectedVersion, async (step, ct) =>
        {
            // The run is staged, not saved: it commits with PENDING_VALIDATION, or not at all (M-11). The submitter is its requester,
            // so WF-11 keeps them from validating their own resolution, and gives no external user authority (ADR-013).
            AdministrationResult<ApprovalInstanceDetail> run = await approvals.StartAsync(
                new ApprovalStart(
                    new ApprovalSubject(ConcernApprovalRouting.SubjectModule, ConcernApprovalRouting.SubjectType, concernId, step.Concern.RevisionNo),
                    ConcernApprovalRouting.ResolutionRoutingKey,
                    callerId,
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
            step.Concern.Resolution = resolution;
            step.Stage(() => ConcernAudit.Transition(ConcernAuditEvents.ResolutionSubmitted, callerId, step.Project, step.From, step.Concern, runId));
            return null;
        }, cancellationToken);
    }

    public Task<AdministrationResult<Versioned<ConcernDetail>>> ReviewAsync(Guid callerId, Guid concernId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, concernId, ConcernCommand.Review, expectedVersion, async (step, ct) =>
        {
            DateOnly before = step.Concern.NextReviewDate;
            step.Concern.NextReviewDate = await references.NextReviewDateAsync(step.Project, step.Now, ct).ConfigureAwait(false);
            step.Concern.LastReviewedAt = step.Now;
            step.Stage(() => ConcernAudit.Reviewed(callerId, step.Project, before, step.Concern));
            return null;
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<ConcernDetail>>> CloseAsync(Guid callerId, Guid concernId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, concernId, ConcernCommand.Close, expectedVersion, async (step, ct) =>
        {
            // An escalation is someone's open obligation: it is resolved or withdrawn before the concern closes.
            if ((await repository.ListOpenEscalationsAsync([concernId], ct).ConfigureAwait(false)).Count > 0)
            {
                return AdministrationError.Conflict(ConcernErrorCodes.EscalationOpen);
            }

            step.Concern.ClosedAt = step.Now;
            step.Stage(() => ConcernAudit.Transition(ConcernAuditEvents.ConcernClosed, callerId, step.Project, step.From, step.Concern));
            return null;
        }, cancellationToken);

    /// <summary>
    /// The shared path of every command: the permission and the person, the project's state, the edge, then the command's own rules under
    /// one unit of work, the new status, and the save. The command's audit event is staged only once its rules hold.
    /// </summary>
    private async Task<AdministrationResult<Versioned<ConcernDetail>>> RunAsync(
        Guid callerId, Guid concernId, ConcernCommand command, uint? expectedVersion, Func<ConcernStep, CancellationToken, Task<AdministrationError?>> apply,
        CancellationToken cancellationToken)
    {
        LoadedConcern loaded = await gate.LoadInternalAsync(callerId, PermissionCatalogue.ConcernManage, concernId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        ConcernEntity concern = loaded.Concern!;
        if (ConcernReferences.ChangeRefused(project) is { } notEligible)
        {
            return notEligible;
        }

        if (ConcernWorkflow.TargetOf(command, concern.Status) is not { } to)
        {
            return ConcernWorkflow.RefusalOf(command, concern.Status) switch
            {
                (true, _) => AdministrationError.Conflict(ConcernErrorCodes.Closed),
                (_, true) => AdministrationError.Conflict(ConcernErrorCodes.NotEditable),
                _ => AdministrationError.InvalidTransition,
            };
        }

        await using IConcernWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        ConcernStep step = new(project, concern, concern.Status, timeProvider.GetUtcNow(), audit);
        if (await apply(step, cancellationToken).ConfigureAwait(false) is { } broken)
        {
            return broken;
        }

        concern.Status = to;
        ConcernGate.Touch(concern, callerId, step.Now);
        step.Flush();
        return await gate.SaveConcernAsync(work, concern, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>One command on one concern, as its rules see it. Its audit event is built after the new status is set, so it records the move whole.</summary>
internal sealed class ConcernStep(ProjectFacts project, ConcernEntity concern, ConcernStatus from, DateTimeOffset now, IAuditTrail audit)
{
    private Func<AuditEntry>? _entry;

    public ProjectFacts Project => project;

    public ConcernEntity Concern => concern;

    public ConcernStatus From => from;

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
