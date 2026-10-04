using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.FinancialKpi;

namespace PMPlatform.Application.Features.FinancialKpi.EventHandlers;

/// <summary>
/// Applies the outcome of a commitment's or a KPI target's WF-11 run (ADR-003 §8.2 edges 26, 28). An approval makes the version
/// ACTIVE and supersedes the ACTIVE version of the same project and type, or of the same assignment, which keeps everything else
/// it held; a return, rejection or withdrawal ends the run as WF-11 decided it. It runs inside the outbox dispatch transaction,
/// so it commits exactly when the delivery mark does.
/// </summary>
/// <remarks>
/// Idempotent on its own as well (EV-4, EV-5): an outcome applies only to the revision under review while it is SUBMITTED, and
/// applying it leaves that state, so the same outcome again, or one for an older revision, is audited as ignored.
/// </remarks>
internal sealed class FinancialKpiApprovalOutcomeHandler(
    IFinancialKpiRepository repository, IProjectFactsReader projects, IAuditTrail audit, TimeProvider timeProvider) : IApprovalOutcomeHandler
{
    public string SubjectModule => FinancialKpiApprovalRouting.SubjectModule;

    public async Task HandleAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        await using IFinancialKpiWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Subject subject = outcome.Subject.Type switch
        {
            FinancialKpiApprovalRouting.CommitmentType => await CommitmentAsync(outcome, cancellationToken).ConfigureAwait(false),
            FinancialKpiApprovalRouting.TargetVersionType => await TargetAsync(outcome, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names an unknown FinancialKpi subject {outcome.Subject.Type}."),
        };

        // Throwing rolls the dispatch back, and the outcome is delivered again later (TASK-035 D-8).
        FinancialKpiSaveOutcome saved = await ApplyAsync(subject, outcome, cancellationToken).ConfigureAwait(false);
        if (saved != FinancialKpiSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} could not be applied: {saved}.");
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves twice on an approval: the previous ACTIVE version leaves ACTIVE first, because the partial unique index on ACTIVE rows
    /// is checked row by row; then this version enters it. The dispatch commits both or neither.
    /// </summary>
    private async Task<FinancialKpiSaveOutcome> ApplyAsync(Subject subject, ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        IApprovedVersion version = subject.Version;
        if (version.Status != ApprovedVersionStatus.Submitted || version.RevisionNo != outcome.Subject.RevisionNo)
        {
            audit.Stage(FinancialKpiAudit.OutcomeIgnored(subject.Project, subject.Facts(version), version.Status, outcome));
            return await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        (Guid decidedBy, DateTimeOffset now) = (outcome.Data.DecidedByUserId, timeProvider.GetUtcNow());
        ApprovedVersionStatus to = ApprovedVersionWorkflow.OutcomeOf(outcome.Data.Decision);
        IApprovedVersion? prior = null;
        if (to == ApprovedVersionStatus.Active)
        {
            prior = await subject.FindActiveAsync(cancellationToken).ConfigureAwait(false);
            if (prior is not null)
            {
                prior.Status = ApprovedVersionStatus.Superseded;
                prior.SupersededById = version.Id;
                Touch(prior, decidedBy, now);
                audit.Stage(FinancialKpiAudit.VersionSuperseded(decidedBy, subject.Project, subject.Facts(prior), version.Id));
                if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) is not FinancialKpiSaveOutcome.Saved and var superseding)
                {
                    return superseding;
                }
            }

            version.ActivatedAt = now;
            version.EffectiveFrom ??= DateOnly.FromDateTime(now.UtcDateTime);
        }

        version.Status = to;
        Touch(version, decidedBy, now);
        audit.Stage(FinancialKpiAudit.OutcomeApplied(subject.Project, subject.Facts(version), to, outcome, prior?.Id));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Subject> CommitmentAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        FinancialCommitment commitment = await repository.FindCommitmentAsync(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false)
                                         ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no commitment.");
        return new Subject(
            await ProjectAsync(commitment.ProjectId, cancellationToken).ConfigureAwait(false),
            commitment,
            v => VersionFacts.Of((FinancialCommitment)v),
            async ct => await repository.FindActiveCommitmentAsync(commitment.ProjectId, commitment.CommitmentType, track: true, ct).ConfigureAwait(false));
    }

    private async Task<Subject> TargetAsync(ApprovalOutcomeRecorded outcome, CancellationToken cancellationToken)
    {
        KpiTargetVersion target = await repository.FindTargetAsync(outcome.Subject.Id, null, cancellationToken).ConfigureAwait(false)
                                  ?? throw new InvalidOperationException($"Approval outcome {outcome.IdempotencyKey} names no KPI target version.");
        KpiAssignment assignment = await repository.FindAssignmentAsync(target.KpiAssignmentId, null, cancellationToken).ConfigureAwait(false)
                                   ?? throw new InvalidOperationException($"Target version {target.Id} names no assignment.");
        return new Subject(
            await ProjectAsync(assignment.ProjectId, cancellationToken).ConfigureAwait(false),
            target,
            v => VersionFacts.Of((KpiTargetVersion)v),
            async ct => await repository.FindActiveTargetAsync(target.KpiAssignmentId, track: true, ct).ConfigureAwait(false));
    }

    private async Task<ProjectFacts> ProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await projects.FindAsync(projectId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException($"Project {projectId} does not exist.");

    private static void Touch(IApprovedVersion version, Guid by, DateTimeOffset at)
    {
        version.UpdatedAt = at;
        version.UpdatedBy = by;
    }

    /// <summary>The version an outcome names, its project, how its audit events state it, and how its ACTIVE predecessor is found.</summary>
    private sealed record Subject(
        ProjectFacts Project, IApprovedVersion Version, Func<IApprovedVersion, VersionFacts> Facts, Func<CancellationToken, Task<IApprovedVersion?>> FindActiveAsync);
}
