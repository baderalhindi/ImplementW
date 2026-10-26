using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// SCR-057 (FG-06 §7): the Activity lines of the projects the caller reaches under <see cref="PermissionCatalogue.ActivityView"/>
/// now, filtered in the query (AUD-CC-16, BR-AUD-025); an external user only ever reaches their own entity's (AUD-CC-18). For an
/// external viewer, an actor outside their entity is not named: AHDA's internal reviewers and approvers stay unnamed (§7.4).
/// </summary>
internal sealed class ActivityService(IAuditReadRepository repository, IAuthorizationEngine engine) : IActivityService
{
    public async Task<ActivityEntryPage> ListAsync(Guid callerId, ActivityQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);

        RecordScope scope = await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ActivityView, cancellationToken).ConfigureAwait(false);
        if (scope.IsEmpty)
        {
            return new ActivityEntryPage([], page.Page, page.PageSize, 0);
        }

        AuthorizationPrincipal? viewer = await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false);
        (IReadOnlyList<AnchoredActivityEntry> items, int total) = await repository.PageActivityAsync(scope, query, page, cancellationToken).ConfigureAwait(false);
        return new ActivityEntryPage([.. items.Select(item => View(item, viewer))], page.Page, page.PageSize, total);
    }

    private static ActivityEntryView View(AnchoredActivityEntry item, AuthorizationPrincipal? viewer)
    {
        bool actorShown = viewer is not { UserType: UserType.External }
                          || (item.ActorUserType == UserType.External && item.ActorExternalEntityId is { } entity && entity == viewer.ExternalEntityId);
        return new ActivityEntryView(
            item.Entry.Id, item.Entry.AuditEventId, item.Entry.ProjectId, item.Entry.OccurredAt, item.Entry.ActivityKind, item.EventType,
            item.Entry.SummaryAr, item.Entry.SummaryEn,
            actorShown ? item.Entry.ActorUserId : null, actorShown ? item.ActorDisplayName : null,
            item.SubjectModule, item.SubjectType, item.SubjectId);
    }
}
