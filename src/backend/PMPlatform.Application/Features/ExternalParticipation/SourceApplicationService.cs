using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// WF-13 §7 and §20.4 (TASK-066): applying an accepted revision to its source record, one attempt at a time, each in one transaction with the
/// source module's change — so a source changed by nothing but its own module, atomically, or not at all (EXT-CC-17):
/// <list type="bullet">
/// <item>the adapter revalidates the record under its module's rules and lock: a version other than the one expected is a CONFLICT that writes
/// nothing to the source, and stays one until an AHDA user revalidates it; the module's own refusal is FAILED, terminal when the record can
/// never take it;</item>
/// <item>every attempt touches its revision's row, so two attempts at one revision cannot both commit; a revision has at most one APPLIED
/// attempt, which the database holds too; and a repeated <c>Idempotency-Key</c> finds its attempt and applies nothing again (EXT-CC-18).</item>
/// </list>
/// </summary>
internal sealed class SourceApplicationService(
    IExternalParticipationRepository repository,
    ExternalParticipationGate gate,
    ContributionTargets targets,
    IAuditTrail audit,
    IAuditRequestContext requestContext,
    TimeProvider timeProvider) : ISourceApplicationService
{
    public async Task<SourceApplicationPage> ListAsync(Guid callerId, Guid contributionId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ExternalContribution? contribution = await repository.FindContributionAsync(contributionId, null, cancellationToken).ConfigureAwait(false);
        if (contribution is null
            || await gate.ViewableRequestAsync(callerId, contribution.ExternalUpdateRequestId, cancellationToken).ConfigureAwait(false) is not var (request, _, audience)
            || audience != ParticipationAudience.Internal)
        {
            return new SourceApplicationPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<SourceApplication> items, int total) = await repository.PageApplicationsAsync(contributionId, page, cancellationToken).ConfigureAwait(false);
        return new SourceApplicationPage([.. items.Select(a => ExternalParticipationViews.ApplicationOf(a, request))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<SourceApplicationDetail>>> GetAsync(Guid callerId, Guid applicationId, CancellationToken cancellationToken)
    {
        SourceApplication? application = await repository.FindApplicationAsync(applicationId, null, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return AdministrationError.NotFound;
        }

        LoadedContribution loaded = await gate.LoadContributionAsync(
            callerId, PermissionCatalogue.ExternalRequestView, application.ExternalContributionId, null, ParticipationActor.Holder, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused
            : loaded.Audience != ParticipationAudience.Internal ? AdministrationError.NotFound
            : new Versioned<SourceApplicationDetail>(ExternalParticipationViews.ApplicationOf(application, loaded.Request!), repository.RowVersionOf(application));
    }

    public async Task<AdministrationResult<SourceApplicationOutcome>> ApplyAsync(Guid callerId, Guid contributionId, Guid requestKey, CancellationToken cancellationToken)
    {
        string idempotencyKey = KeyOf(callerId, requestKey);
        if (await ReplayAsync(callerId, contributionId, idempotencyKey, cancellationToken).ConfigureAwait(false) is { } replayed)
        {
            return replayed;
        }

        LoadedContribution loaded = await gate.LoadContributionAsync(
            callerId, PermissionCatalogue.ExternalContributionApply, contributionId, null, ParticipationActor.Holder, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ExternalUpdateRequest request, ExternalContribution contribution) = (loaded.Request!, loaded.Contribution!);
        if (contribution.Status != ExternalContributionStatus.AcceptedPendingApplication)
        {
            return AdministrationError.Conflict(contribution.Status == ExternalContributionStatus.Applied
                ? ExternalParticipationErrorCodes.ApplicationAlreadyCompleted
                : ExternalParticipationErrorCodes.ApplicationNotPermitted);
        }

        IReadOnlyList<SourceApplication> attempts = await repository.ListApplicationsAsync(contribution.Id, cancellationToken).ConfigureAwait(false);
        if (attempts is [.., { Status: SourceApplicationStatus.Conflict, RevalidatedAt: null }])
        {
            return AdministrationError.Conflict(ExternalParticipationErrorCodes.ApplicationConflict);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        SourceApplication application = new()
        {
            Id = Guid.CreateVersion7(now),
            ExternalContributionId = contribution.Id,
            AttemptNo = attempts.Count + 1,
            IdempotencyKey = idempotencyKey,
            CorrelationId = requestContext.CorrelationId,
            ExpectedTargetRevisionNo = attempts.LastOrDefault(a => a.RevalidatedTargetRevisionNo is not null)?.RevalidatedTargetRevisionNo ?? contribution.TargetVersion,
            AttemptedByUserId = callerId,
            AttemptedAt = now,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        Dictionary<string, string> values = (await repository.ListFieldsAsync([contribution.Id], cancellationToken).ConfigureAwait(false))
            .ToDictionary(f => f.FieldCode, f => f.ProposedValue, StringComparer.Ordinal);
        IExternalContributionTarget target = targets.Of(ContributionSchemas.Of(request.ContributionSchemaCode))
                                             ?? throw new InvalidOperationException($"Contribution {contribution.Id} awaits application but its schema applies to no source.");

        ExternalParticipationSaveOutcome saved;
        await using (IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            SourceApplicationResult result = await target.StageAsync(
                    new SourceApplicationCommand(
                        request.TargetId!.Value, application.ExpectedTargetRevisionNo!.Value, values, callerId, request.ExternalEntityId, contribution.Id,
                        contribution.RevisionNo, application.Id),
                    cancellationToken)
                .ConfigureAwait(false);

            DateTimeOffset completed = timeProvider.GetUtcNow();
            application.Status = result.Status;
            application.ActualTargetRevisionNo = result.ActualVersion;
            application.FailureCode = result.FailureCode;
            application.CompletedAt = completed;
            ExternalContributionStatus from = contribution.Status;
            if (result.Status == SourceApplicationStatus.Applied || result.Terminal)
            {
                contribution.Status = result.Status == SourceApplicationStatus.Applied ? ExternalContributionStatus.Applied : ExternalContributionStatus.ApplicationFailed;
                request.Status = ExternalUpdateRequestStatus.Closed;
                request.ClosedAt = completed;
                ExternalParticipationGate.Touch(request, callerId, completed);
            }

            // Every attempt touches its revision, so a concurrent attempt at the same revision fails on its row version.
            ExternalParticipationGate.Touch(contribution, callerId, completed);
            repository.Add(application);
            audit.Stage(ExternalParticipationAudit.Attempt(callerId, request, contribution, application, from));
            saved = await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false);
        }

        // Another attempt committed first: the same request retried finds its own; anything else finds the revision applied or moved on.
        return saved == ExternalParticipationSaveOutcome.Saved
            ? new SourceApplicationOutcome(ExternalParticipationViews.ApplicationOf(application, request), Replayed: false)
            : await ReplayAsync(callerId, contributionId, idempotencyKey, cancellationToken).ConfigureAwait(false)
              ?? (await repository.FindContributionAsync(contributionId, null, cancellationToken).ConfigureAwait(false) is { Status: ExternalContributionStatus.Applied }
                  ? AdministrationError.Conflict(ExternalParticipationErrorCodes.ApplicationAlreadyCompleted)
                  : AdministrationError.PreconditionFailed);
    }

    public async Task<AdministrationResult<Versioned<SourceApplicationDetail>>> RevalidateAsync(
        Guid callerId, Guid applicationId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        SourceApplication? application = await repository.FindApplicationAsync(applicationId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return AdministrationError.NotFound;
        }

        LoadedContribution loaded = await gate.LoadContributionAsync(
            callerId, PermissionCatalogue.ExternalContributionApply, application.ExternalContributionId, null, ParticipationActor.Holder, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ExternalUpdateRequest request, ExternalContribution contribution) = (loaded.Request!, loaded.Contribution!);
        IReadOnlyList<SourceApplication> attempts = await repository.ListApplicationsAsync(contribution.Id, cancellationToken).ConfigureAwait(false);
        if (application is not { Status: SourceApplicationStatus.Conflict, RevalidatedAt: null }
            || attempts[^1].Id != application.Id
            || contribution.Status != ExternalContributionStatus.AcceptedPendingApplication)
        {
            return AdministrationError.Conflict(ExternalParticipationErrorCodes.ApplicationNotRevalidatable);
        }

        IExternalContributionTarget target = targets.Of(ContributionSchemas.Of(request.ContributionSchemaCode))!;
        if (await target.FindAsync(request.TargetId!.Value, cancellationToken).ConfigureAwait(false) is not { } source)
        {
            return AdministrationError.Rule(ExternalParticipationErrorCodes.SourceRecordNotFound);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        application.RevalidatedAt = now;
        application.RevalidatedByUserId = callerId;
        application.RevalidatedTargetRevisionNo = source.Version;
        ExternalParticipationGate.Touch(application, callerId, now);
        ExternalParticipationGate.Touch(contribution, callerId, now);

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(ExternalParticipationAudit.Revalidated(callerId, request, contribution, application));
        return await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == ExternalParticipationSaveOutcome.Saved
            ? new Versioned<SourceApplicationDetail>(ExternalParticipationViews.ApplicationOf(application, request), repository.RowVersionOf(application))
            : AdministrationError.PreconditionFailed;
    }

    /// <summary>R-37 scopes a key to its principal.</summary>
    private static string KeyOf(Guid callerId, Guid requestKey) => $"{callerId:N}:{requestKey:N}";

    /// <summary>
    /// The attempt this key made, as it is now, when it was made for this revision and the caller may still see it (R-47); 422 when the key
    /// made an attempt for another; null when it made none.
    /// </summary>
    private async Task<AdministrationResult<SourceApplicationOutcome>?> ReplayAsync(Guid callerId, Guid contributionId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (await repository.FindApplicationByKeyAsync(idempotencyKey, cancellationToken).ConfigureAwait(false) is not { } earlier)
        {
            return null;
        }

        if (earlier.ExternalContributionId != contributionId)
        {
            return AdministrationError.Rule(ExternalParticipationErrorCodes.IdempotencyKeyReused);
        }

        ExternalContribution contribution = (await repository.FindContributionAsync(contributionId, null, cancellationToken).ConfigureAwait(false))!;
        return await gate.ViewableRequestAsync(callerId, contribution.ExternalUpdateRequestId, cancellationToken).ConfigureAwait(false) is ({ } request, _, ParticipationAudience.Internal)
            ? new SourceApplicationOutcome(ExternalParticipationViews.ApplicationOf(earlier, request), Replayed: true)
            : AdministrationError.NotFound;
    }
}
