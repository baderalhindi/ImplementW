using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Application.Features.Risk.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Risk;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// A risk's moves along <see cref="RiskWorkflow"/> (TASK-055). Each command is decided on its own permission — rating and
/// acceptance as AHDA's authority, internal users only (ADR-013) — finds its edge in the state machine or is refused 409, applies
/// its own rules, and saves the risk with what it wrote in one transaction. A closed risk is refused every command but the reopen.
/// </summary>
internal sealed class RiskLifecycleService(
    IRiskRepository repository, RiskGate gate, IConfigurationResolver configuration, IRiskIssueMaterialisation issues, IAuditTrail audit, TimeProvider timeProvider)
    : IRiskLifecycleService
{
    public Task<AdministrationResult<Versioned<RiskDetail>>> AssessAsync(
        Guid callerId, Guid riskId, RiskAssessmentDraft draft, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return RunAsync(callerId, riskId, RiskCommand.Assess, expectedVersion, (step, ct) => AssessAsync(step, draft, ct), cancellationToken);
    }

    public Task<AdministrationResult<Versioned<RiskDetail>>> StartTreatmentAsync(Guid callerId, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, riskId, RiskCommand.StartTreatment, expectedVersion, async (step, ct) =>
        {
            // An accepted risk is tolerated, not treated: the acceptance is revoked first, by its authority.
            if (await repository.FindActiveAcceptanceAsync(riskId, ct).ConfigureAwait(false) is not null)
            {
                return AdministrationError.Conflict(RiskErrorCodes.AcceptanceActive);
            }

            if (!await repository.HasOpenActionAsync(riskId, ct).ConfigureAwait(false))
            {
                return AdministrationError.Rule(RiskErrorCodes.TreatmentActionRequired);
            }

            step.Stage(() => RiskAudit.Transition(RiskAuditEvents.TreatmentStarted, callerId, AuditActorType.User, step.Project, step.From, step.Risk));
            return null;
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<RiskDetail>>> MonitorAsync(Guid callerId, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, riskId, RiskCommand.Monitor, expectedVersion, (step, _) =>
        {
            step.Stage(() => RiskAudit.Transition(RiskAuditEvents.MonitoringStarted, callerId, AuditActorType.User, step.Project, step.From, step.Risk));
            return Task.FromResult<AdministrationError?>(null);
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<RiskDetail>>> AcceptAsync(
        Guid callerId, Guid riskId, RiskAcceptanceDraft draft, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return RunAsync(callerId, riskId, RiskCommand.Accept, expectedVersion, async (step, ct) =>
        {
            // No permanent acceptance (TASK-055 gate decision): it expires, at the latest, on the date given, which is after today.
            if (draft.ExpiresOn <= DateOnly.FromDateTime(step.Now.UtcDateTime))
            {
                return AdministrationError.Rule(RiskErrorCodes.AcceptanceExpiryInvalid, new FieldIssue("expiresOn", FieldIssue.NotAllowed));
            }

            if (await repository.FindActiveAcceptanceAsync(riskId, ct).ConfigureAwait(false) is not null)
            {
                return AdministrationError.Conflict(RiskErrorCodes.AcceptanceActive);
            }

            RiskAcceptance acceptance = new()
            {
                Id = Guid.CreateVersion7(step.Now),
                RiskId = riskId,
                AcceptedByUserId = callerId,
                AcceptedAt = step.Now,
                ExpiresOn = draft.ExpiresOn,
                Rationale = draft.Rationale,
                Status = RiskAcceptanceStatus.Active,
                CreatedAt = step.Now,
                CreatedBy = callerId,
                UpdatedAt = step.Now,
                UpdatedBy = callerId,
            };
            repository.Add(acceptance);

            // The risk returns for review on the day its acceptance lapses.
            step.Risk.NextReviewDate = draft.ExpiresOn;
            step.Stage(() => RiskAudit.Transition(RiskAuditEvents.RiskAccepted, callerId, AuditActorType.User, step.Project, step.From, step.Risk, acceptance));
            return null;
        }, cancellationToken);
    }

    public Task<AdministrationResult<Versioned<RiskDetail>>> RevokeAcceptanceAsync(Guid callerId, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, riskId, RiskCommand.RevokeAcceptance, expectedVersion, async (step, ct) =>
        {
            if (await repository.FindActiveAcceptanceAsync(riskId, ct).ConfigureAwait(false) is not { } acceptance)
            {
                return AdministrationError.Conflict(RiskErrorCodes.AcceptanceNotActive);
            }

            Revoke(acceptance, callerId, step.Now);
            step.Stage(() => RiskAudit.Transition(RiskAuditEvents.AcceptanceRevoked, callerId, AuditActorType.User, step.Project, step.From, step.Risk, acceptance));
            return null;
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<RiskDetail>>> CloseAsync(
        Guid callerId, Guid riskId, NarrativeText rationale, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rationale);
        return RunAsync(callerId, riskId, RiskCommand.Close, expectedVersion, async (step, ct) =>
        {
            // An acceptance tolerates a risk that exists; a closed one is not tolerated any more, so the acceptance ends with it.
            RiskAcceptance? acceptance = await repository.FindActiveAcceptanceAsync(riskId, ct).ConfigureAwait(false);
            if (acceptance is not null)
            {
                Revoke(acceptance, callerId, step.Now);
            }

            step.Risk.ClosureRationale = rationale;
            step.Risk.ClosedAt = step.Now;
            step.Risk.ClosedByUserId = callerId;
            step.Stage(() => RiskAudit.Transition(RiskAuditEvents.RiskClosed, callerId, AuditActorType.User, step.Project, step.From, step.Risk, acceptance));
            return null;
        }, cancellationToken);
    }

    public Task<AdministrationResult<Versioned<RiskDetail>>> ReopenAsync(Guid callerId, Guid riskId, uint? expectedVersion, CancellationToken cancellationToken) =>
        RunAsync(callerId, riskId, RiskCommand.Reopen, expectedVersion, (step, _) =>
        {
            step.Risk.ClosureRationale = null;
            step.Risk.ClosedAt = null;
            step.Risk.ClosedByUserId = null;
            step.Risk.ReopenedCount++;
            step.Stage(() => RiskAudit.Transition(RiskAuditEvents.RiskReopened, callerId, AuditActorType.User, step.Project, step.From, step.Risk));
            return Task.FromResult<AdministrationError?>(null);
        }, cancellationToken);

    public Task<AdministrationResult<Versioned<RiskDetail>>> MaterialiseAsync(
        Guid callerId, Guid riskId, RiskMaterialisationDraft draft, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return RunAsync(callerId, riskId, RiskCommand.Materialise, expectedVersion, async (step, ct) =>
        {
            if (step.Risk.MaterialisedAt is not null)
            {
                return AdministrationError.Conflict(RiskErrorCodes.AlreadyMaterialised);
            }

            // The issue carries the risk's latest impacts, on the dimension set risks and issues share (ADR-011).
            IReadOnlyList<RiskAssessmentVersion> latest = await repository.ListLatestAssessmentsAsync([riskId], ct).ConfigureAwait(false);
            IReadOnlyList<RiskAssessmentImpact> impacts = latest.Count == 0 ? [] : await repository.ListImpactsAsync([latest[0].Id], ct).ConfigureAwait(false);
            AdministrationResult<OriginatedIssue> raised = await issues.RaiseIssueAsync(
                new RiskIssueCommand(
                    callerId, step.Project.Id, riskId, draft.Title ?? step.Risk.Title, draft.Description ?? step.Risk.Description, draft.CategoryItemId, draft.PriorityItemId,
                    [.. impacts.Select(i => new RiskIssueImpact(i.ImpactDimensionItemId, i.ImpactLevel))], step.Now),
                ct).ConfigureAwait(false);
            if (!raised.Succeeded)
            {
                return raised.Error;
            }

            Guid concernId = raised.Value.Id;
            step.Risk.MaterialisedAt = step.Now;
            step.Stage(() => RiskAudit.Materialised(callerId, step.Project, step.Risk, concernId));
            return null;
        }, cancellationToken);
    }

    /// <summary>
    /// The assessment (ADR-011, PTBC-017): rated by the RISK_MATRIX version in force now, whose id, overall impact and rating are
    /// stored with it, so a later publication never changes what it recorded. A matrix that is not published fails closed.
    /// </summary>
    private async Task<AdministrationError?> AssessAsync(RiskStep step, RiskAssessmentDraft draft, CancellationToken cancellationToken)
    {
        ResolvedConfiguration matrix = await configuration.ResolveAsync(ConfigurationFamilyCodes.RiskMatrix, step.Now, cancellationToken).ConfigureAwait(false);
        if (RiskRating.Check(matrix.Content, draft) is { Count: > 0 } issues)
        {
            return AdministrationError.Rule(RiskErrorCodes.ImpactInvalid, [.. issues]);
        }

        short overall = RiskRating.OverallImpactOf(draft.Impacts);
        RiskRatingEntry rating = matrix.RequireRiskRating(draft.ProbabilityLevel, overall);
        RiskRatingReference pinned = (await configuration.ListRiskRatingsAsync(matrix.VersionId, cancellationToken).ConfigureAwait(false)).SingleOrDefault(r => r.Code == rating.Code)
                                     ?? throw new ConfigurationMissingException(ConfigurationFamilyCodes.RiskMatrix, ConfigurationMissingReason.EntryMissing, $"risk rating {rating.Code}");
        IReadOnlyList<RiskAssessmentVersion> previous = await repository.ListLatestAssessmentsAsync([step.Risk.Id], cancellationToken).ConfigureAwait(false);
        RiskAssessmentVersion assessment = new()
        {
            Id = Guid.CreateVersion7(step.Now),
            RiskId = step.Risk.Id,
            VersionNo = previous.Count == 0 ? 1 : previous[0].VersionNo + 1,
            AssessedAt = step.Now,
            AssessedByUserId = step.ActorId,
            MatrixConfigurationVersionId = matrix.VersionId,
            ProbabilityLevel = draft.ProbabilityLevel,
            OverallImpactLevel = overall,
            RiskRatingDefinitionId = pinned.Id,
            Rationale = draft.Rationale,
            CreatedAt = step.Now,
            CreatedBy = step.ActorId,
            UpdatedAt = step.Now,
            UpdatedBy = step.ActorId,
        };
        repository.Add(assessment);
        foreach (RiskImpactInput impact in draft.Impacts)
        {
            repository.Add(new RiskAssessmentImpact
            {
                Id = Guid.CreateVersion7(step.Now),
                RiskAssessmentVersionId = assessment.Id,
                ImpactDimensionItemId = impact.ImpactDimensionItemId,
                ImpactLevel = impact.ImpactLevel,
                Rationale = impact.Rationale,
                CreatedAt = step.Now,
                CreatedBy = step.ActorId,
                UpdatedAt = step.Now,
                UpdatedBy = step.ActorId,
            });
        }

        step.Stage(() => RiskAudit.Assessed(step.ActorId, step.Project, step.From, step.Risk, assessment, pinned.Code));
        return null;
    }

    /// <summary>
    /// The shared path of every command: the permission, the project's state, the edge, then the command's own rules under one unit
    /// of work, the new status, and the save. The command's audit event is staged only once its rules hold.
    /// </summary>
    private async Task<AdministrationResult<Versioned<RiskDetail>>> RunAsync(
        Guid callerId, Guid riskId, RiskCommand command, uint? expectedVersion, Func<RiskStep, CancellationToken, Task<AdministrationError?>> apply, CancellationToken cancellationToken)
    {
        string permission = RiskWorkflow.PermissionOf(command);
        LoadedRisk loaded = command is RiskCommand.Assess or RiskCommand.Accept or RiskCommand.RevokeAcceptance
            ? await gate.LoadWithAuthorityAsync(callerId, permission, riskId, expectedVersion, cancellationToken).ConfigureAwait(false)
            : await gate.LoadRiskAsync(callerId, permission, riskId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ProjectFacts project = loaded.Project!;
        RiskEntity risk = loaded.Risk!;
        if (RiskReferences.ChangeRefused(project) is { } notEligible)
        {
            return notEligible;
        }

        bool assessed = command == RiskCommand.Reopen && (await repository.ListLatestAssessmentsAsync([riskId], cancellationToken).ConfigureAwait(false)).Count > 0;
        if (RiskWorkflow.TargetOf(command, risk.Status, assessed) is not { } to)
        {
            return RiskWorkflow.IsOpen(risk.Status) ? AdministrationError.InvalidTransition : AdministrationError.Conflict(RiskErrorCodes.Closed);
        }

        await using IRiskWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        RiskStep step = new(callerId, project, risk, risk.Status, timeProvider.GetUtcNow(), audit);
        if (await apply(step, cancellationToken).ConfigureAwait(false) is { } broken)
        {
            return broken;
        }

        risk.Status = to;
        RiskGate.Touch(risk, callerId, step.Now);
        step.Flush();
        return await gate.SaveRiskAsync(work, risk, cancellationToken).ConfigureAwait(false);
    }

    private static void Revoke(RiskAcceptance acceptance, Guid actorId, DateTimeOffset now)
    {
        acceptance.Status = RiskAcceptanceStatus.Revoked;
        acceptance.RevokedAt = now;
        RiskGate.Touch(acceptance, actorId, now);
    }
}

/// <summary>
/// One command on one risk, as its rules see it: who, which risk and project, the state it was in, and when. Its audit event is
/// built after the new status is set, so it records the move whole.
/// </summary>
internal sealed class RiskStep(Guid actorId, ProjectFacts project, RiskEntity risk, RiskStatus from, DateTimeOffset now, IAuditTrail audit)
{
    private Func<AuditEntry>? _entry;

    public Guid ActorId => actorId;

    public ProjectFacts Project => project;

    public RiskEntity Risk => risk;

    public RiskStatus From => from;

    public DateTimeOffset Now => now;

    /// <summary>Stages <paramref name="entry"/> once the move is applied; built then, so it sees the risk's new status.</summary>
    public void Stage(Func<AuditEntry> entry) => _entry = entry;

    public void Flush()
    {
        if (_entry is not null)
        {
            audit.Stage(_entry());
        }
    }
}
