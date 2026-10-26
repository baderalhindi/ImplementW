using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// The scheduled activation of WF-09 (§4.1, DCL-SUS-03): each APPROVED request whose effective date has come is activated as WF-09's
/// service principal, its eligibility read again at that moment (SUS-CC-23). One request per unit of work, so one refusal touches no other;
/// a refused request stays APPROVED and is tried again on the next pass. Two instances activating the same request cannot both commit:
/// each changes the request's and the project's rows, and the project's open suspension is unique.
/// </summary>
internal sealed class SuspensionMaintenance(
    ISuspensionRepository repository, IProjectFactsReader projects, SuspensionGate gate, SuspensionActivation activation, TimeProvider timeProvider) : ISuspensionMaintenance
{
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        int effected = 0;
        foreach (Guid due in await repository.ListDueAsync(DateOnly.FromDateTime(now.UtcDateTime), batchSize, cancellationToken).ConfigureAwait(false))
        {
            effected += await ActivateAsync(due, now, cancellationToken).ConfigureAwait(false) ? 1 : 0;
        }

        return effected;
    }

    private async Task<bool> ActivateAsync(Guid suspensionRequestId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using ISuspensionWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        SuspensionRequest? request = await repository.FindAsync(suspensionRequestId, null, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = request is null ? null : await projects.FindAsync(request.ProjectId, cancellationToken).ConfigureAwait(false);
        return request?.Status == SuspensionRequestStatus.Approved && project is not null
               && await activation.StageAsync(request, project, SuspensionServicePrincipal.Id, AuditActorType.Service, now, cancellationToken).ConfigureAwait(false) is null
               && await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == SuspensionSaveOutcome.Saved;
    }
}
