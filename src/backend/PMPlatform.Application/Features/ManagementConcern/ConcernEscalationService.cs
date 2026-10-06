using System.Globalization;
using System.Text.Json;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.ManagementConcern.Contracts.Events;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ManagementConcern;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// Escalations (TASK-057; WF-07 §5.3). An escalation is a record of its own beside the concern's lifecycle, never a status of it and
/// never an approval (ISS-GP-06). It is raised by an internal user (ADR-013), addressed to the role WORKFLOW_POLICY's
/// <c>CONCERN_ESCALATION_ROLE</c> names when it is raised, and published to WF-15 as exactly one NotificationIntent:
/// <list type="bullet">
/// <item>the intent is staged in the outbox in the transaction that records the escalation, keyed by the escalation's id — the outbox's
/// unique message key refuses a second, and WF-15 records one intent per source reference however often it is delivered;</item>
/// <item>the escalation keeps the escalator's <c>Idempotency-Key</c>, so a retried request finds it and publishes nothing (R-37), and a
/// concern holds one OPEN escalation at a time, so a second request with a new key is refused, not published.</item>
/// </list>
/// </summary>
internal sealed class ConcernEscalationService(
    IManagementConcernRepository repository, ConcernGate gate, ConcernAccess access, IProjectFactsReader projects, IConfigurationResolver configuration,
    IMasterDataResolver masterData, IRoleDirectory roles, IOutbox outbox, IAuditTrail audit, IAuditRequestContext request, TimeProvider timeProvider)
    : IConcernEscalationService
{
    /// <summary>The WORKFLOW_POLICY value naming the role a concern escalation is addressed to (TEXT, a role code); no default.</summary>
    public const string EscalationRoleKey = "CONCERN_ESCALATION_ROLE";

    public async Task<AdministrationResult<EscalationOutcome>> EscalateAsync(Guid callerId, ConcernEscalationDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (await ReplayAsync(callerId, draft, cancellationToken).ConfigureAwait(false) is { } replayed)
        {
            return replayed;
        }

        LoadedConcern loaded = await gate.LoadInternalAsync(callerId, PermissionCatalogue.ConcernEscalate, draft.ManagementConcernId, null, cancellationToken).ConfigureAwait(false);
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

        if (!ConcernWorkflow.IsActive(concern.Status))
        {
            return AdministrationError.Conflict(ConcernErrorCodes.NotEscalatable);
        }

        if ((await repository.ListOpenEscalationsAsync([concern.Id], cancellationToken).ConfigureAwait(false)).Count > 0)
        {
            return AdministrationError.Conflict(ConcernErrorCodes.EscalationOpen);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        RoleSummary route = await RouteAsync(now, cancellationToken).ConfigureAwait(false);
        ConcernEscalation escalation = new()
        {
            Id = Guid.CreateVersion7(now),
            ManagementConcernId = concern.Id,
            EscalationNo = await repository.NextEscalationNoAsync(concern.Id, cancellationToken).ConfigureAwait(false),
            EscalatedByUserId = callerId,
            EscalatedAt = now,
            EscalatedToRoleId = route.Id,
            Reason = draft.Reason,
            Status = ConcernEscalationStatus.Open,
            RequestKey = draft.RequestKey,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        ConcernEscalated intent = await IntentAsync(callerId, project, concern, escalation, route, now, cancellationToken).ConfigureAwait(false);

        ConcernSaveOutcome saved;
        await using (IConcernWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            repository.Add(escalation);
            audit.Stage(ConcernAudit.Escalation(ConcernAuditEvents.ConcernEscalated, callerId, project, concern, escalation));
            outbox.Stage(intent);
            saved = await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false);
        }

        // A unique key taken meanwhile: the same request, retried while the first was saving, finds what the first raised; anything
        // else is another escalation that is now OPEN. Either way nothing more is published.
        return saved == ConcernSaveOutcome.Saved
            ? new EscalationOutcome(ConcernViews.ToDetail(escalation), Replayed: false)
            : await ReplayAsync(callerId, draft, cancellationToken).ConfigureAwait(false) ?? AdministrationError.Conflict(ConcernErrorCodes.EscalationOpen);
    }

    public Task<AdministrationResult<ConcernEscalationDetail>> ResolveAsync(Guid callerId, Guid escalationId, NarrativeText resolution, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return EndAsync(callerId, escalationId, ConcernEscalationStatus.Resolved, resolution, cancellationToken);
    }

    public Task<AdministrationResult<ConcernEscalationDetail>> WithdrawAsync(Guid callerId, Guid escalationId, CancellationToken cancellationToken) =>
        EndAsync(callerId, escalationId, ConcernEscalationStatus.Withdrawn, null, cancellationToken);

    public async Task<AdministrationResult<ConcernEscalationDetail>> GetAsync(Guid callerId, Guid escalationId, CancellationToken cancellationToken) =>
        await FindViewableAsync(callerId, escalationId, cancellationToken).ConfigureAwait(false) is var (escalation, _, _)
            ? ConcernViews.ToDetail(escalation)
            : AdministrationError.NotFound;

    public async Task<ConcernEscalationPage> ListAsync(Guid callerId, Guid concernId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (await gate.ViewableConcernAsync(callerId, concernId, cancellationToken).ConfigureAwait(false) is null)
        {
            return new ConcernEscalationPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<ConcernEscalation> items, int total) = await repository.PageEscalationsAsync(concernId, page, cancellationToken).ConfigureAwait(false);
        return new ConcernEscalationPage([.. items.Select(ConcernViews.ToDetail)], page.Page, page.PageSize, total);
    }

    /// <summary>
    /// R-37 for the escalator's key: the escalation it raised, as it is now and published once, when the request is the same — the same
    /// concern and reason — and the caller may still see it (R-47); 422 when the key was used for another; null when it raised nothing yet.
    /// </summary>
    private async Task<AdministrationResult<EscalationOutcome>?> ReplayAsync(Guid callerId, ConcernEscalationDraft draft, CancellationToken cancellationToken)
    {
        ConcernEscalation? earlier = await repository.FindEscalationByRequestKeyAsync(callerId, draft.RequestKey, cancellationToken).ConfigureAwait(false);
        return earlier is null ? null
            : earlier.ManagementConcernId != draft.ManagementConcernId || earlier.Reason != draft.Reason ? AdministrationError.Rule(ConcernErrorCodes.IdempotencyKeyReused)
            : await gate.ViewableConcernAsync(callerId, earlier.ManagementConcernId, cancellationToken).ConfigureAwait(false) is null ? AdministrationError.NotFound
            : new EscalationOutcome(ConcernViews.ToDetail(earlier), Replayed: true);
    }

    /// <summary>The role WORKFLOW_POLICY in force addresses escalations to; a missing value or an unknown role fails closed (422 CONFIGURATION_MISSING).</summary>
    private async Task<RoleSummary> RouteAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        ResolvedConfiguration policy = await configuration.ResolveAsync(ConfigurationFamilyCodes.WorkflowPolicy, now, cancellationToken).ConfigureAwait(false);
        string roleCode = policy.RequireText(EscalationRoleKey);
        return (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).SingleOrDefault(r => r.Code == roleCode)
               ?? throw new ConfigurationMissingException(ConfigurationFamilyCodes.WorkflowPolicy, ConfigurationMissingReason.EntryInvalid, $"value {EscalationRoleKey}");
    }

    /// <summary>
    /// The escalation's NotificationIntent (EV-10): routed by WF-15 from the CONCERN_ESCALATION family and the project's anchors; it
    /// names no recipient and carries no free text, only codes and the deep link.
    /// </summary>
    private async Task<ConcernEscalated> IntentAsync(
        Guid callerId, ProjectFacts project, ConcernEntity concern, ConcernEscalation escalation, RoleSummary route, DateTimeOffset now, CancellationToken cancellationToken)
    {
        string severity = concern.SeverityItemId is { } severityItemId
            ? (await masterData.ListPublishedItemsAsync(MasterDataCatalogueCodes.ConcernSeverity, cancellationToken).ConfigureAwait(false))
              .SingleOrDefault(i => i.Id == severityItemId)?.Code ?? string.Empty
            : string.Empty;
        string key = escalation.Id.ToString();
        return new ConcernEscalated
        {
            EventId = Guid.CreateVersion7(now),
            EventType = ConcernEscalated.Type,
            SchemaVersion = ConcernEscalated.CurrentSchemaVersion,
            Kind = EventKind.NotificationIntent,
            MessageKey = EventMessageKey.Of(ConcernEscalated.Type, key),
            IdempotencyKey = key,
            OccurredAt = now,
            SourceModule = ConcernAudit.Module,
            CorrelationId = request.CorrelationId,
            Actor = new EventActor(AuditActorType.User, callerId),
            Subject = new EventSubject(ConcernAudit.Module, ConcernAudit.ConcernType, concern.Id, concern.RevisionNo),
            Scope = new EventScope(project.Id, project.DepartmentId, project.ExternalEntityId),
            Data = new NotificationIntentData(
                ConcernEscalated.EventFamilyCode,
                key,
                null,
                $"/projects/{project.Id}/issues-challenges/{concern.Id}",
                [
                    new NotificationParameter("concernType", JsonNamingPolicy.SnakeCaseUpper.ConvertName(concern.ConcernType.ToString())),
                    new NotificationParameter("escalationNo", escalation.EscalationNo.ToString(CultureInfo.InvariantCulture)),
                    new NotificationParameter("escalatedToRoleCode", route.Code),
                    new NotificationParameter("severity", severity),
                ],
                null),
        };
    }

    /// <summary>
    /// Ends an OPEN escalation. Resolving it is its addressee's: CONCERN_ESCALATION_RESOLVE held through the role it is addressed to —
    /// routing is assignment, not rank. Withdrawing it is its escalator's own, under CONCERN_ESCALATE. Internal users only, both.
    /// </summary>
    private async Task<AdministrationResult<ConcernEscalationDetail>> EndAsync(
        Guid callerId, Guid escalationId, ConcernEscalationStatus status, NarrativeText? resolution, CancellationToken cancellationToken)
    {
        if (await FindViewableAsync(callerId, escalationId, cancellationToken).ConfigureAwait(false) is not var (escalation, concern, project))
        {
            return AdministrationError.NotFound;
        }

        bool resolving = status == ConcernEscalationStatus.Resolved;
        string permission = resolving ? PermissionCatalogue.ConcernEscalationResolve : PermissionCatalogue.ConcernEscalate;
        string? roleCode = resolving ? (await roles.ListRolesAsync(cancellationToken).ConfigureAwait(false)).Single(r => r.Id == escalation.EscalatedToRoleId).Code : null;
        AdministrationError? refused = await access.CheckInternalAsync(
                callerId, permission, project, concern.AssigneeUserId,
                reason => ConcernAudit.AuthorityRefused(callerId, project, concern.Id, permission, reason), cancellationToken, roleCode)
            .ConfigureAwait(false)
            ?? (resolving || escalation.EscalatedByUserId == callerId ? null : AdministrationError.Forbidden)
            ?? (escalation.Status == ConcernEscalationStatus.Open ? null : AdministrationError.Conflict(ConcernErrorCodes.EscalationNotOpen));
        if (refused is not null)
        {
            return refused;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        escalation.Status = status;
        escalation.ResolvedAt = now;
        escalation.ResolvedByUserId = callerId;
        escalation.Resolution = resolution;
        ConcernGate.Touch(escalation, callerId, now);
        await using IConcernWork work = await repository.BeginAsync(cancellationToken).ConfigureAwait(false);
        audit.Stage(ConcernAudit.Escalation(
            resolving ? ConcernAuditEvents.EscalationResolved : ConcernAuditEvents.EscalationWithdrawn, callerId, project, concern, escalation));

        // A concurrent resolution or withdrawal changed the row first: this one ended nothing.
        return await gate.SaveAsync(work, cancellationToken).ConfigureAwait(false) == ConcernSaveOutcome.Saved
            ? ConcernViews.ToDetail(escalation)
            : AdministrationError.Conflict(ConcernErrorCodes.EscalationNotOpen);
    }

    /// <summary>The escalation, tracked, with its concern and project, if the caller may see the project's concerns (R-47).</summary>
    private async Task<(ConcernEscalation Escalation, ConcernEntity Concern, ProjectFacts Project)?> FindViewableAsync(
        Guid callerId, Guid escalationId, CancellationToken cancellationToken)
    {
        if (await repository.FindEscalationAsync(escalationId, cancellationToken).ConfigureAwait(false) is not { } escalation)
        {
            return null;
        }

        ConcernEntity concern = await repository.FindAsync(escalation.ManagementConcernId, null, cancellationToken).ConfigureAwait(false)
                                ?? throw new InvalidOperationException($"Escalation {escalationId} names no concern.");
        ProjectFacts project = await projects.FindAsync(concern.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Concern {concern.Id} names no project.");
        return await access.CanViewAsync(callerId, project, concern.AssigneeUserId, cancellationToken).ConfigureAwait(false) ? (escalation, concern, project) : null;
    }
}
