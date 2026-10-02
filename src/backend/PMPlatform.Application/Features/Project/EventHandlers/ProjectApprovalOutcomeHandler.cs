using System.Globalization;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.EventHandlers;

/// <summary>
/// Applies the outcome of a registration review (ADR-003 §8.2 edge 28): APPROVED moves the project to APPROVED_PLANNED and
/// issues its Formal Project ID; RETURNED, REJECTED and WITHDRAWN move it to RETURNED, from which it is resubmitted as the
/// next revision. It runs inside the outbox dispatch transaction, so the change commits exactly when the delivery mark does.
/// </summary>
/// <remarks>
/// It is idempotent on its own as well (EV-4): an outcome applies only to the revision under review while it is UNDER_REVIEW,
/// and applying it moves the project out of that state, so the same outcome delivered again — or one for an older revision
/// (EV-5) — finds nothing to apply and is audited as ignored. REJECTED has no state of its own: ERD §6 has no cancelled or
/// rejected project (ERD E-3), and the workbook names RETURNED and APPROVED_PLANNED as the only ends of review.
/// </remarks>
internal sealed class ProjectApprovalOutcomeHandler(IProjectRepository repository, IAuditTrail audit, TimeProvider timeProvider) : IApprovalOutcomeHandler
{
    public string SubjectModule => ProjectApprovalRouting.SubjectModule;

    /// <summary>The one authoritative identifier's format (project-registration.md F-3): <c>PRJ-</c> and six digits of its sequence number.</summary>
    public static string FormalProjectIdOf(long number) => string.Create(CultureInfo.InvariantCulture, $"PRJ-{number:D6}");

    public async Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        ProjectEntity project = await repository.FindAsync(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false)
                                ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no project.");

        if (project.RevisionNo != outcome.Subject.RevisionNo || project.LifecycleState != ProjectLifecycleState.UnderReview)
        {
            audit.Stage(ProjectAudit.OutcomeIgnored(project, outcome));
        }
        else if (outcome.Data.Decision == ApprovalOutcomeDecision.Approved)
        {
            project.FormalProjectId = FormalProjectIdOf(await repository.NextFormalProjectNumberAsync(cancellationToken).ConfigureAwait(false));
            project.LifecycleState = ProjectLifecycleState.ApprovedPlanned;
            Touch(project, outcome);
            audit.Stage(ProjectAudit.Approved(project, outcome));
        }
        else
        {
            project.LifecycleState = ProjectLifecycleState.Returned;
            Touch(project, outcome);
            audit.Stage(ProjectAudit.Returned(project, outcome));
        }

        // Throwing rolls the dispatch back, and the outcome is delivered again later (TASK-035 D-8).
        ProjectSaveOutcome saved = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (saved != ProjectSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} could not be applied: {saved}.");
        }
    }

    /// <summary>The decision's author made the change: the last approver, the returner or rejecter, or the requester who withdrew.</summary>
    private void Touch(ProjectEntity project, ApprovalOutcomeRecorded outcome)
    {
        project.UpdatedAt = timeProvider.GetUtcNow();
        project.UpdatedBy = outcome.Data.DecidedByUserId;
    }
}
