using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// The lifecycle activation of an APPROVED request (WF-09 §9.1, §9.2): the one place a project's ACTIVE ↔ SUSPENDED transition is made,
/// and never by the approval. It revalidates the project's state and the open suspension on the rows it reads, then stages, in the
/// caller's unit of work, the project's transition (edge 7), the suspension period opened or ended, and the request EFFECTED with its own
/// audit event. Nothing reaches Schedule: the Approved Baseline, the forecast and every due date are as they were (BR-SUS-008,
/// BR-SUS-023); a rebaseline after resumption is WF-08's authorisation applied by WF-03 (BR-SUS-024). A refusal stages nothing, so the
/// request stays APPROVED and the project as it was (BR-SUS-021, BR-SUS-022).
/// </summary>
internal sealed class SuspensionActivation(ISuspensionRepository repository, IProjectSuspensionCommands lifecycle, IAuditTrail audit)
{
    /// <summary>Null when staged; the caller's save commits it, and the project's row version fails that save if the project moved meanwhile.</summary>
    public async Task<AdministrationError?> StageAsync(
        SuspensionRequest request, ProjectFacts project, Guid actorId, AuditActorType actorType, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(project);
        if (request.RequestedEffectiveDate > DateOnly.FromDateTime(now.UtcDateTime))
        {
            return AdministrationError.Conflict(SuspensionErrorCodes.NotYetEffective);
        }

        if (SuspensionEligibility.ProjectRefused(request.RequestType, project) is { } notEligible)
        {
            return notEligible;
        }

        ActiveSuspension? open = await repository.FindOpenSuspensionAsync(project.Id, cancellationToken).ConfigureAwait(false);
        ProjectSuspensionCommand command = new(project.Id, actorId, actorType, request.Id);
        ActiveSuspension suspension;
        if (request.RequestType == SuspensionRequestType.Suspend)
        {
            if (open is not null)
            {
                return SuspensionEligibility.DuplicateOf(SuspensionRequestType.Suspend);
            }

            if (await lifecycle.SuspendAsync(command, cancellationToken).ConfigureAwait(false) is not null)
            {
                return AdministrationError.Rule(SuspensionErrorCodes.ProjectNotEligible);
            }

            suspension = new ActiveSuspension
            {
                Id = Guid.CreateVersion7(now),
                ProjectId = project.Id,
                SuspensionRequestId = request.Id,
                StartedAt = now,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            };
            repository.Add(suspension);
        }
        else
        {
            if (open is null || await lifecycle.ResumeAsync(command, cancellationToken).ConfigureAwait(false) is not null)
            {
                return AdministrationError.Rule(SuspensionErrorCodes.ProjectNotEligible);
            }

            suspension = open;
            suspension.EndedAt = now;
            suspension.ResumptionRequestId = request.Id;
            SuspensionGate.Touch(suspension, actorId, now);
        }

        request.Status = SuspensionRequestStatus.Effected;
        request.EffectedAt = now;
        SuspensionGate.Touch(request, actorId, now);
        audit.Stage(SuspensionAudit.Effected(actorId, actorType, project, request, suspension));
        return null;
    }
}
