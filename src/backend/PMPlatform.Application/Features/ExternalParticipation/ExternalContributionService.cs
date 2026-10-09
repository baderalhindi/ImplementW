using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.ExternalParticipation.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// WF-13 Path B's answers (TASK-066). The responder drafts revision 1 of an ISSUED request and submits it complete; from then on its values
/// never change — no operation here writes a value of a revision past DRAFT, and the database refuses one (D-11). AHDA's assigned reviewer
/// starts the review and decides the revision as a whole: accept, return — which opens the next revision as a DRAFT holding the same values
/// for the responder to correct — or reject. Acceptance is neither an approval nor an application (BR-EXT-014, BR-EXT-015).
/// </summary>
internal sealed class ExternalContributionService(
    IExternalParticipationRepository repository,
    ExternalParticipationGate gate,
    ExternalParticipationViews views,
    ContributionTargets targets,
    IAuditTrail audit,
    TimeProvider timeProvider) : IExternalContributionService
{
    public async Task<ExternalContributionPage> ListAsync(Guid callerId, Guid requestId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableRequestAsync(callerId, requestId, cancellationToken).ConfigureAwait(false) is not var (_, _, audience))
        {
            return new ExternalContributionPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ExternalContribution> items, int total) = await repository.PageRevisionsAsync(requestId, page, cancellationToken).ConfigureAwait(false);
        return new ExternalContributionPage(await views.ContributionsAsync(items, audience, cancellationToken).ConfigureAwait(false), page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<ExternalContributionDetail>>> GetAsync(Guid callerId, Guid contributionId, CancellationToken cancellationToken)
    {
        LoadedContribution loaded = await gate.LoadContributionAsync(
            callerId, PermissionCatalogue.ExternalRequestView, contributionId, null, ParticipationActor.Holder, cancellationToken).ConfigureAwait(false);
        return loaded.Error is { } refused ? refused : await VersionedAsync(loaded.Contribution!, loaded.Audience, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ExternalContributionDetail>>> StartAsync(
        Guid callerId, Guid requestId, IReadOnlyList<ContributionFieldInput> fields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);
        LoadedRequest loaded = await gate.LoadRequestAsync(
            callerId, PermissionCatalogue.ExternalContributionRespond, requestId, null, ParticipationActor.Responder, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        ExternalUpdateRequest request = loaded.Request!;
        if (request.Status != ExternalUpdateRequestStatus.Issued)
        {
            return AdministrationError.Conflict(ExternalParticipationErrorCodes.RequestNotOpen);
        }

        AdministrationResult<IReadOnlyList<ContributionFieldValue>> values = ContributionValues.Normalize(ContributionSchemas.Of(request.ContributionSchemaCode), fields);
        if (!values.Succeeded)
        {
            return values.Error;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ExternalContribution contribution = NewRevision(request, callerId, revisionNo: 1, previous: null, now);
        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        repository.Add(contribution);
        AddFields(contribution, values.Value, callerId, now);
        request.Status = ExternalUpdateRequestStatus.InProgress;
        ExternalParticipationGate.Touch(request, callerId, now);
        audit.Stage(ExternalParticipationAudit.ContributionValues(
            ExternalParticipationAuditEvents.ContributionStarted, callerId, request, contribution, values.Value.Select(v => v.FieldCode)));

        // The request moved meanwhile, or another first revision took the open-revision key: the request is no longer open for one.
        return await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == ExternalParticipationSaveOutcome.Saved
            ? await VersionedAsync(contribution, loaded.Audience, cancellationToken).ConfigureAwait(false)
            : AdministrationError.Conflict(ExternalParticipationErrorCodes.RequestNotOpen);
    }

    public async Task<AdministrationResult<Versioned<ExternalContributionDetail>>> UpdateAsync(
        Guid callerId, Guid contributionId, IReadOnlyList<ContributionFieldInput> fields, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);
        LoadedContribution loaded = await gate.LoadContributionAsync(
            callerId, PermissionCatalogue.ExternalContributionRespond, contributionId, expectedVersion, ParticipationActor.Responder, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ExternalUpdateRequest request, ExternalContribution contribution) = (loaded.Request!, loaded.Contribution!);
        AdministrationError? notEditable = contribution.Status != ExternalContributionStatus.Draft
            ? AdministrationError.Conflict(ExternalParticipationErrorCodes.ContributionNotEditable)
            : request.Status != ExternalUpdateRequestStatus.InProgress ? AdministrationError.Conflict(ExternalParticipationErrorCodes.RequestNotOpen)
            : null;
        if (notEditable is not null)
        {
            return notEditable;
        }

        AdministrationResult<IReadOnlyList<ContributionFieldValue>> values = ContributionValues.Normalize(ContributionSchemas.Of(request.ContributionSchemaCode), fields);
        if (!values.Succeeded)
        {
            return values.Error;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, ExternalContributionField> stored = (await repository.FindFieldsAsync(contribution.Id, cancellationToken).ConfigureAwait(false))
            .ToDictionary(f => f.FieldCode, StringComparer.Ordinal);
        foreach (ExternalContributionField gone in stored.Values.Where(f => values.Value.All(v => v.FieldCode != f.FieldCode)))
        {
            repository.Remove(gone);
        }

        foreach (ContributionFieldValue value in values.Value)
        {
            if (stored.TryGetValue(value.FieldCode, out ExternalContributionField? field))
            {
                field.ProposedValue = value.Value;
                field.ProposedValueLanguage = value.Language;
                ExternalParticipationGate.Touch(field, callerId, now);
            }
            else
            {
                AddFields(contribution, [value], callerId, now);
            }
        }

        contribution.ContributorUserId = callerId;
        ExternalParticipationGate.Touch(contribution, callerId, now);
        audit.Stage(ExternalParticipationAudit.ContributionValues(
            ExternalParticipationAuditEvents.ContributionChanged, callerId, request, contribution, values.Value.Select(v => v.FieldCode)));
        return await SaveContributionAsync(work, contribution, loaded.Audience, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<ExternalContributionDetail>>> SubmitAsync(
        Guid callerId, Guid contributionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        LoadedContribution loaded = await gate.LoadContributionAsync(
            callerId, PermissionCatalogue.ExternalContributionRespond, contributionId, expectedVersion, ParticipationActor.Responder, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ExternalUpdateRequest request, ExternalContribution contribution) = (loaded.Request!, loaded.Contribution!);
        ContributionSchema schema = ContributionSchemas.Of(request.ContributionSchemaCode);
        AdministrationResult<StatusOf> next = ExternalParticipationWorkflow.TargetOf(ContributionCommand.Submit, contribution.Status, schema.Mode);
        if (!next.Succeeded)
        {
            return next.Error;
        }

        if (request.Status != ExternalUpdateRequestStatus.InProgress)
        {
            return AdministrationError.Conflict(ExternalParticipationErrorCodes.RequestNotOpen);
        }

        IReadOnlyList<ExternalContributionField> fields = await repository.ListFieldsAsync([contribution.Id], cancellationToken).ConfigureAwait(false);
        if (ContributionValues.MissingRequired(schema, fields.Select(f => f.FieldCode)) is { } incomplete)
        {
            return incomplete;
        }

        // EXT-F-073, EXT-F-074: the source as it is when the answer is given, which its application will expect to find (BR-EXT-021).
        if (targets.Of(schema) is { } target)
        {
            SourceRecordFacts? source = await target.FindAsync(request.TargetId!.Value, cancellationToken).ConfigureAwait(false);
            if (source is null || source.ProjectId != request.ProjectId)
            {
                return AdministrationError.Rule(ExternalParticipationErrorCodes.SourceInvalid, new FieldIssue("targetId", FieldIssue.NotFound));
            }

            contribution.TargetVersion = source.Version;
            contribution.TargetState = source.State;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        contribution.Status = next.Value.Status;
        contribution.SubmittedAt = now;
        contribution.ContributorUserId = callerId;
        ExternalParticipationGate.Touch(contribution, callerId, now);
        request.Status = ExternalUpdateRequestStatus.Responded;
        ExternalParticipationGate.Touch(request, callerId, now);

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(ExternalParticipationAudit.ContributionTransition(
            ExternalParticipationAuditEvents.ContributionSubmitted, callerId, ExternalContributionStatus.Draft, request, contribution));
        return await SaveContributionAsync(work, contribution, loaded.Audience, cancellationToken).ConfigureAwait(false);
    }

    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> StartReviewAsync(
        Guid callerId, Guid contributionId, uint? expectedVersion, CancellationToken cancellationToken) =>
        DecideAsync(callerId, contributionId, ContributionCommand.StartReview, new ContributionReviewDecision(null, null), expectedVersion, cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> AcceptAsync(
        Guid callerId, Guid contributionId, ContributionReviewDecision decision, uint? expectedVersion, CancellationToken cancellationToken) =>
        DecideAsync(callerId, contributionId, ContributionCommand.Accept, decision, expectedVersion, cancellationToken);

    /// <summary>A return names its reason for the entity (WF-13 §4.5 step 7); the API refuses one without.</summary>
    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> ReturnAsync(
        Guid callerId, Guid contributionId, ContributionReviewDecision decision, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(decision.Reason, nameof(decision));
        return DecideAsync(callerId, contributionId, ContributionCommand.Return, decision, expectedVersion, cancellationToken);
    }

    /// <summary>A rejection names its reason for the entity (US-EXT-EE-022); the API refuses one without.</summary>
    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> RejectAsync(
        Guid callerId, Guid contributionId, ContributionReviewDecision decision, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(decision.Reason, nameof(decision));
        return DecideAsync(callerId, contributionId, ContributionCommand.Reject, decision, expectedVersion, cancellationToken);
    }

    /// <summary>
    /// The review: the request's assigned reviewer, an AHDA user, never the revision's contributor (EXT-CC-10). A decision is on the revision
    /// as a whole and writes only the review's own columns and the status; the submitted values are not touched (BR-EXT-012, BR-EXT-018).
    /// </summary>
    private async Task<AdministrationResult<Versioned<ExternalContributionDetail>>> DecideAsync(
        Guid callerId, Guid contributionId, ContributionCommand command, ContributionReviewDecision decision, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        LoadedContribution loaded = await gate.LoadContributionAsync(
            callerId, PermissionCatalogue.ExternalContributionReview, contributionId, expectedVersion, ParticipationActor.Reviewer, cancellationToken).ConfigureAwait(false);
        if (loaded.Error is { } refused)
        {
            return refused;
        }

        (ExternalUpdateRequest request, ExternalContribution contribution) = (loaded.Request!, loaded.Contribution!);
        if (contribution.ContributorUserId == callerId)
        {
            return AdministrationError.Forbidden;
        }

        AdministrationResult<StatusOf> next = ExternalParticipationWorkflow.TargetOf(command, contribution.Status, ContributionSchemas.Of(request.ContributionSchemaCode).Mode);
        if (!next.Succeeded)
        {
            return next.Error;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ExternalContributionStatus from = contribution.Status;
        contribution.Status = next.Value.Status;
        ExternalParticipationGate.Touch(contribution, callerId, now);
        if (command == ContributionCommand.StartReview)
        {
            contribution.ReviewStartedAt = now;
        }
        else
        {
            contribution.ReviewedByUserId = callerId;
            contribution.ReviewedAt = now;
            contribution.ReviewReason = decision.Reason;
            contribution.ReviewInternalNote = decision.InternalNote;
        }

        await using IExternalParticipationWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        Guid? nextRevisionId = null;
        switch (contribution.Status)
        {
            case ExternalContributionStatus.Returned:
                nextRevisionId = await OpenNextRevisionAsync(request, contribution, callerId, now, cancellationToken).ConfigureAwait(false);
                request.Status = ExternalUpdateRequestStatus.InProgress;
                ExternalParticipationGate.Touch(request, callerId, now);
                break;
            case ExternalContributionStatus.Rejected:
            case ExternalContributionStatus.Applied:
                request.Status = ExternalUpdateRequestStatus.Closed;
                request.ClosedAt = now;
                ExternalParticipationGate.Touch(request, callerId, now);
                break;
            case ExternalContributionStatus.UnderReview:
            case ExternalContributionStatus.AcceptedPendingApplication:
                break;
            case ExternalContributionStatus.Draft:
            case ExternalContributionStatus.Submitted:
            case ExternalContributionStatus.ApplicationFailed:
            default:
                throw new InvalidOperationException($"A review does not lead to {contribution.Status}.");
        }

        audit.Stage(ExternalParticipationAudit.ContributionTransition(EventOf(command), callerId, from, request, contribution, nextRevisionId));
        return await SaveContributionAsync(work, contribution, loaded.Audience, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// WF-13 §6.1: a return opens the next revision, a DRAFT holding the returned values for the request's responder to correct. The returned
    /// revision stays as it was submitted.
    /// </summary>
    private async Task<Guid> OpenNextRevisionAsync(ExternalUpdateRequest request, ExternalContribution returned, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ExternalContribution next = NewRevision(request, request.ResponsibleUserId!.Value, returned.RevisionNo + 1, returned.Id, now);
        next.CreatedBy = actorId;
        next.UpdatedBy = actorId;
        repository.Add(next);
        AddFields(
            next,
            [.. (await repository.ListFieldsAsync([returned.Id], cancellationToken).ConfigureAwait(false)).Select(f => new ContributionFieldValue(f.FieldCode, f.ProposedValue, f.ProposedValueLanguage))],
            actorId,
            now);
        return next.Id;
    }

    private static ExternalContribution NewRevision(ExternalUpdateRequest request, Guid contributorUserId, int revisionNo, Guid? previous, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            ExternalUpdateRequestId = request.Id,
            ProjectId = request.ProjectId,
            ExternalEntityId = request.ExternalEntityId,
            ContributorUserId = contributorUserId,
            RevisionNo = revisionNo,
            Status = ExternalContributionStatus.Draft,
            PreviousRevisionId = previous,
            CreatedAt = now,
            CreatedBy = contributorUserId,
            UpdatedAt = now,
            UpdatedBy = contributorUserId,
        };

    private void AddFields(ExternalContribution contribution, IEnumerable<ContributionFieldValue> values, Guid actorId, DateTimeOffset now)
    {
        foreach (ContributionFieldValue value in values)
        {
            repository.Add(new ExternalContributionField
            {
                Id = Guid.CreateVersion7(now),
                ExternalContributionId = contribution.Id,
                FieldCode = value.FieldCode,
                ProposedValue = value.Value,
                ProposedValueLanguage = value.Language,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
            });
        }
    }

    private static string EventOf(ContributionCommand command) => command switch
    {
        ContributionCommand.StartReview => ExternalParticipationAuditEvents.ReviewStarted,
        ContributionCommand.Accept => ExternalParticipationAuditEvents.ContributionAccepted,
        ContributionCommand.Return => ExternalParticipationAuditEvents.ContributionReturned,
        ContributionCommand.Reject => ExternalParticipationAuditEvents.ContributionRejected,
        ContributionCommand.Submit => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a review command."),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a review command."),
    };

    /// <summary>Saves, commits and answers with the revision as now saved. A row changed meanwhile is a stale version (412).</summary>
    private async Task<AdministrationResult<Versioned<ExternalContributionDetail>>> SaveContributionAsync(
        IExternalParticipationWork work, ExternalContribution contribution, ParticipationAudience audience, CancellationToken cancellationToken) =>
        await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == ExternalParticipationSaveOutcome.Saved
            ? await VersionedAsync(contribution, audience, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;

    private async Task<Versioned<ExternalContributionDetail>> VersionedAsync(ExternalContribution contribution, ParticipationAudience audience, CancellationToken cancellationToken) =>
        new(await views.ContributionAsync(contribution, audience, cancellationToken).ConfigureAwait(false), repository.RowVersionOf(contribution));
}
