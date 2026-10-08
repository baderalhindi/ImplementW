using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Risk.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// No permanent acceptance (TASK-055 gate decision): an ACTIVE acceptance whose expiry has come is marked EXPIRED, and its risk
/// returns to ASSESSED for review, its next review due on the expiry date. One acceptance per save, so one failure touches no
/// other; two instances expiring the same one cannot both commit, as each changes the risk's row. A CLOSED project is read-only
/// (WF-10, TASK-063): its accepted risks are history and are left as they are, and the pass steps past them, so they never take
/// the place of another project's acceptance in a batch.
/// </summary>
internal sealed class RiskMaintenance(IRiskRepository repository, IProjectFactsReader projects, RiskGate gate, IAuditTrail audit, TimeProvider timeProvider) : IRiskMaintenance
{
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        int expired = 0;
        RiskAcceptance? after = null;
        while (expired < batchSize)
        {
            IReadOnlyList<RiskAcceptance> lapsed = await repository.ListLapsedAcceptancesAsync(today, after, batchSize, cancellationToken).ConfigureAwait(false);
            foreach (RiskAcceptance acceptance in lapsed)
            {
                expired += await ExpireAsync(acceptance, now, cancellationToken).ConfigureAwait(false) ? 1 : 0;
            }

            if (lapsed.Count < batchSize)
            {
                break;
            }

            after = lapsed[^1];
        }

        return expired;
    }

    private async Task<bool> ExpireAsync(RiskAcceptance lapsed, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using IRiskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        RiskAcceptance? acceptance = await repository.FindActiveAcceptanceAsync(lapsed.RiskId, cancellationToken).ConfigureAwait(false);
        RiskEntity? risk = await repository.FindRiskAsync(lapsed.RiskId, null, cancellationToken).ConfigureAwait(false);
        ProjectFacts? project = risk is null ? null : await projects.FindAsync(risk.ProjectId, cancellationToken).ConfigureAwait(false);
        if (acceptance?.Id != lapsed.Id || risk is null || project is null || project.Status == ProjectLifecycleState.Closed)
        {
            return false;
        }

        Guid actor = RiskServicePrincipal.Id;
        RiskStatus from = risk.Status;
        acceptance.Status = RiskAcceptanceStatus.Expired;
        RiskGate.Touch(acceptance, actor, now);
        risk.Status = RiskWorkflow.TargetOf(RiskCommand.ExpireAcceptance, from, assessed: true) ?? from;
        risk.NextReviewDate = acceptance.ExpiresOn;
        RiskGate.Touch(risk, actor, now);
        audit.Stage(RiskAudit.Transition(RiskAuditEvents.AcceptanceExpired, actor, AuditActorType.Service, project, from, risk, acceptance));
        return await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false);
    }
}
