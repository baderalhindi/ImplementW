using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// The lifecycle activation of an APPROVED case (WF-10 §5 C07, C12): the one place a project becomes COMPLETED or CLOSED, and never by
/// the approval. Immediately before commit it revalidates the project's state and the readiness (BR-CLO-034): a criterion that fails now
/// and is not waived refuses it, leaving the case APPROVED and the project as it was (CLO-CC-25), to be activated once the blocker is
/// cleared. Then, in one transaction (CLO-CC-11, CLO-CC-12): the project's transition (edge 8); on the terminal path the end of its open
/// suspension (edge 45); the readiness it was revalidated against; the case EFFECTED with its own audit event; and, on closure, the end of
/// every per-project access assignment (ADR-013). Nothing reaches a task, milestone, risk, change or baseline (BR-CLO-011 to BR-CLO-017).
/// </summary>
internal sealed class CloseoutActivation(
    ICloseoutRepository repository,
    CloseoutReadiness readiness,
    IProjectCloseoutCommands lifecycle,
    ISuspensionClosureCommands suspensions,
    IProjectAccessLifecycle access,
    IAuditTrail audit)
{
    /// <summary>
    /// Activates the case in <paramref name="work"/> and commits it. Null when committed; otherwise the refusal, and nothing is committed.
    /// The project's and the case's row versions fail the save if either moved meanwhile.
    /// </summary>
    public async Task<AdministrationError?> ActivateAsync(
        ICloseoutWork work, CloseoutCase @case, ProjectFacts project, Guid actorId, AuditActorType actorType, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(@case);
        ArgumentNullException.ThrowIfNull(project);
        if (CloseoutEligibility.ProjectRefused(@case, project) is { } notEligible)
        {
            return notEligible;
        }

        IReadOnlyList<ReadinessCheck> evaluation = await readiness.EvaluateAsync(@case, actorId, now, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ReadinessCheck> recorded = await repository.ListReadinessAsync([@case.Id], cancellationToken).ConfigureAwait(false);
        ReadinessDetail revalidated = ReadinessRollUp.Of([.. recorded, .. evaluation]);
        if (CloseoutEligibility.BlockerOf(revalidated) is { } blocked)
        {
            return blocked;
        }

        ProjectCloseoutCommand command = new(project.Id, actorId, actorType, @case.Id);
        if (@case is ClosureCase { CompletionCaseId: null }
            && await suspensions.EndForClosureAsync(new SuspensionClosureCommand(project.Id, actorId, actorType, @case.Id), cancellationToken).ConfigureAwait(false) is not null)
        {
            return AdministrationError.Rule(ClosureErrorCodes.ProjectNotEligible);
        }

        AdministrationError? moved = @case is CompletionCase
            ? await lifecycle.CompleteAsync(command, cancellationToken).ConfigureAwait(false)
            : await lifecycle.CloseAsync(command, cancellationToken).ConfigureAwait(false);
        if (moved is not null)
        {
            return AdministrationError.Rule(ClosureErrorCodes.ProjectNotEligible);
        }

        foreach (ReadinessCheck row in evaluation)
        {
            repository.Add(row);
        }

        @case.Status = CloseoutCaseStatus.Effected;
        @case.EffectedAt = now;
        CloseoutGate.Touch(@case, actorId, now);
        audit.Stage(CloseoutAudit.Effected(actorId, actorType, project, @case, revalidated));

        CloseoutSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved != CloseoutSaveOutcome.Saved)
        {
            return CloseoutGate.RefusalOf(saved);
        }

        // In the same transaction, after the save that answers a stale version with 412: IdentityAccess owns the assignments (M-1) and
        // saves its own change, which the commit below makes durable with the closure, or not at all.
        if (@case is ClosureCase)
        {
            await access.EndAccessForClosedProjectAsync(project.Id, actorId, cancellationToken).ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }
}
