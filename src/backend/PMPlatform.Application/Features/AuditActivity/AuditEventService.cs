using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// ADM-050 and ADM-051 (FG-06 §6, §8). Audit access is its own authority, distinct from administration (BR-AUD-023): every read
/// asks the engine for the event's category permission, then applies that permission's scope to the event's anchors and its
/// clearance to the event's classification (AUD-CC-02 to -05). Holding R01, or any other category, opens nothing more.
/// </summary>
internal sealed class AuditEventService(IAuditReadRepository repository, IAuthorizationEngine engine, IAuditTrail audit) : IAuditEventService
{
    /// <summary>Attributes that are request context (AUD.CONTEXT.SENSITIVE): shown only with <see cref="PermissionCatalogue.AuditContextView"/>.</summary>
    internal static readonly IReadOnlySet<string> RequestContextAttributes = new HashSet<string>(StringComparer.Ordinal) { AuditTrail.ClientAddressAttribute, "username" };

    public async Task<AuditAccessView> GetAccessAsync(Guid callerId, CancellationToken cancellationToken)
    {
        List<AuditCategory> categories = [];
        foreach (AuditCategory category in AuditCategories.All)
        {
            if (await HoldsAsync(callerId, AuditCategories.PermissionOf(category), cancellationToken).ConfigureAwait(false))
            {
                categories.Add(category);
            }
        }

        return new AuditAccessView(
            categories,
            await HoldsAsync(callerId, PermissionCatalogue.AuditContextView, cancellationToken).ConfigureAwait(false),
            await HoldsAsync(callerId, PermissionCatalogue.ActivityView, cancellationToken).ConfigureAwait(false),
            await HoldsAsync(callerId, PermissionCatalogue.AuditOperationsManage, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AdministrationResult<AuditEventPage>> SearchAsync(Guid callerId, AuditEventQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        List<AuditReach> reach = [];
        foreach (AuditCategory category in query.Categories.Count > 0 ? query.Categories.Distinct() : AuditCategories.All)
        {
            string permission = AuditCategories.PermissionOf(category);
            RecordScope scope = await engine.GetRecordScopeAsync(callerId, permission, cancellationToken).ConfigureAwait(false);
            if (!scope.IsEmpty)
            {
                reach.Add(new AuditReach(category, scope));
            }
            else if (query.Categories.Count > 0)
            {
                // A category asked for by name and not held: the engine refuses it and audits the refusal (CTL-25).
                AuthorizationDecision decision = await engine.AuthorizeAsync(callerId, new AuthorizationRequest(permission), cancellationToken).ConfigureAwait(false);
                if (!decision.IsAllowed)
                {
                    return AdministrationError.Forbidden;
                }
            }
        }

        if (reach.Count == 0)
        {
            return new AuditEventPage([], page.Page, page.PageSize, 0);
        }

        (IReadOnlyList<AnchoredAuditEvent> items, int total) = await repository.PageEventsAsync(reach, query, page, cancellationToken).ConfigureAwait(false);
        return new AuditEventPage([.. items.Select(Summary)], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<AuditEventDetail>> GetAsync(Guid callerId, Guid auditEventId, CancellationToken cancellationToken)
    {
        AnchoredAuditEvent? found = await repository.FindEventAsync(auditEventId, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return AdministrationError.NotFound;
        }

        AuditCategory category = AuditCategories.Of(found.Event);
        AuthorizationDecision decision = await engine.AuthorizeAsync(
            callerId, new AuthorizationRequest(AuditCategories.PermissionOf(category), found.Subject), cancellationToken).ConfigureAwait(false);
        if (!decision.IsAllowed)
        {
            return decision.Outcome == AuthorizationOutcome.Forbidden ? AdministrationError.Forbidden : AdministrationError.NotFound;
        }

        bool contextVisible = await HoldsAsync(callerId, PermissionCatalogue.AuditContextView, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<AuditEventAttribute> attributes = await repository.ListAttributesAsync(auditEventId, cancellationToken).ConfigureAwait(false);
        AuditForwardingRecord? forwarding = await repository.FindForwardingAsync(auditEventId, cancellationToken).ConfigureAwait(false);

        List<AuditEventAttribute> masked = contextVisible ? [] : [.. attributes.Where(a => RequestContextAttributes.Contains(a.AttributeName))];
        bool contextRevealed = contextVisible && attributes.Any(a => RequestContextAttributes.Contains(a.AttributeName));
        if (category == AuditCategory.Security || found.Event.DataClassificationItemId is not null || contextRevealed)
        {
            // FG-06 §6.4: access to sensitive evidence is itself evidence; routine search is not.
            await audit.RecordAsync(AuditActivityAudit.SensitiveDetailViewed(callerId, found.Event, category, contextRevealed)).ConfigureAwait(false);
        }

        AuditEvent e = found.Event;
        return new AuditEventDetail(
            Summary(found),
            [.. attributes.Except(masked).OrderBy(a => a.AttributeName, StringComparer.Ordinal).Select(a => new AuditAttributeView(a.AttributeName, a.OldValue, a.NewValue))],
            [.. masked.Select(a => a.AttributeName).Order(StringComparer.Ordinal)],
            new AuditEventIntegrity(e.EventHash ?? string.Empty, e.PreviousEventHash, e.AttributeDigest),
            forwarding is null ? null : new AuditEventForwarding(forwarding.Status, forwarding.ForwardedAt));
    }

    private async Task<bool> HoldsAsync(Guid callerId, string permission, CancellationToken cancellationToken) =>
        (await engine.EvaluateAsync(callerId, new AuthorizationRequest(permission), cancellationToken).ConfigureAwait(false)).IsAllowed;

    private static AuditEventSummary Summary(AnchoredAuditEvent anchored)
    {
        AuditEvent e = anchored.Event;
        return new AuditEventSummary(
            e.Id, e.OccurredAt, e.RecordedAt, AuditCategories.Of(e), e.EventClass, e.EventType, e.Outcome, e.ActorType, e.ActorUserId, anchored.ActorDisplayName,
            e.SubjectModule, e.SubjectType, e.SubjectId, e.ScopeProjectId, e.ScopeExternalEntityId, e.CorrelationId, e.DataClassificationItemId);
    }
}
