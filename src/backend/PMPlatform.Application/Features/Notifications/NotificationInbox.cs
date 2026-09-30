using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// A person's own notifications and preferences (TASK-039, served to TASK-040). Every read and write is filtered by the
/// caller's id in the query itself, so no one reaches another person's notification. Preferences are stored only for
/// what a recipient may choose — e-mail and SMS of a non-mandatory family — and routing and sending read them on every
/// send, so a change applies to the next one.
/// </summary>
internal sealed class NotificationInbox(
    INotificationRepository repository, IConfigurationResolver configuration, IAuditTrail audit, TimeProvider timeProvider) : INotificationInbox
{
    public async Task<NotificationPage> ListAsync(Guid userId, NotificationInboxQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (items, total) = await repository.ListInboxAsync(userId, query, cancellationToken).ConfigureAwait(false);
        return new NotificationPage([.. items.Select(i => NotificationMapping.ToSummary(i.Delivery, i.Intent))], query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<UnreadNotificationCount> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        new(await repository.CountUnreadAsync(userId, cancellationToken).ConfigureAwait(false));

    public async Task<AdministrationResult<NotificationSummary>> GetAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken) =>
        await repository.FindInboxItemAsync(userId, notificationId, cancellationToken).ConfigureAwait(false) is { } item
            ? NotificationMapping.ToSummary(item.Delivery, item.Intent)
            : AdministrationError.NotFound;

    public async Task<AdministrationResult<NotificationSummary>> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        if (await repository.FindInboxItemAsync(userId, notificationId, cancellationToken).ConfigureAwait(false) is not { } item)
        {
            return AdministrationError.NotFound;
        }

        if (item.Delivery.Status == NotificationDeliveryStatus.Sent)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            item.Delivery.Status = NotificationDeliveryStatus.Read;
            item.Delivery.ReadAt = now;
            item.Delivery.UpdatedAt = now;
            item.Delivery.UpdatedBy = userId;

            // Read by the same person twice at once: either save records it, and the other finds it read.
            await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        }

        return await GetAsync(userId, notificationId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NotificationsMarkedRead> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken) =>
        new(await repository.MarkAllReadAsync(userId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false));

    public async Task<NotificationHistoryPage> ListHistoryAsync(Guid userId, NotificationHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (items, total) = await repository.ListHistoryAsync(userId, query, cancellationToken).ConfigureAwait(false);
        return new NotificationHistoryPage([.. items.Select(i => NotificationMapping.ToHistoryItem(i.Delivery, i.Intent))], query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<NotificationPreferenceSet> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        ResolvedConfiguration routing = await configuration.ResolveAsync(ConfigurationFamilyCodes.NotificationRouting, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        IReadOnlyList<NotificationPreference> stored = await repository.GetPreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
        return new NotificationPreferenceSet([.. routing.Content.NotificationEventFamilies.OrderBy(f => f.Code, StringComparer.Ordinal).Select(family =>
            new NotificationFamilyPreference(family.Code, family.Label, family.IsMandatory, [.. family.Channels.OrderBy(c => c.Channel).Select(channel =>
            {
                bool? preference = stored.SingleOrDefault(p => p.EventFamilyCode == family.Code && p.Channel == channel.Channel)?.IsEnabled;
                return new NotificationChannelPreference(
                    channel.Channel, NotificationRouting.Choice(family, channel, preference) is null, IsConfigurable(family, channel.Channel));
            })]))]);
    }

    public async Task<AdministrationResult<NotificationPreferenceSet>> UpdatePreferencesAsync(
        Guid userId, IReadOnlyList<NotificationPreferenceChange> changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        DateTimeOffset now = timeProvider.GetUtcNow();
        ResolvedConfiguration routing = await configuration.ResolveAsync(ConfigurationFamilyCodes.NotificationRouting, now, cancellationToken).ConfigureAwait(false);

        List<FieldIssue> invalid = [];
        List<FieldIssue> fixedChoices = [];
        for (int i = 0; i < changes.Count; i++)
        {
            NotificationPreferenceChange change = changes[i];
            NotificationEventFamilyEntry? family = routing.Content.NotificationEventFamilies.SingleOrDefault(f => f.Code == change.EventFamilyCode);
            if (family is null || family.Channels.All(c => c.Channel != change.Channel))
            {
                invalid.Add(new FieldIssue(family is null ? $"preferences[{i}].eventFamilyCode" : $"preferences[{i}].channel", FieldIssue.NotFound));
            }
            else if (!IsConfigurable(family, change.Channel))
            {
                fixedChoices.Add(new FieldIssue($"preferences[{i}].channel", FieldIssue.NotAllowed));
            }
        }

        if (invalid.Count > 0)
        {
            return AdministrationError.Rule(NotificationErrorCodes.PreferenceInvalid, [.. invalid]);
        }

        if (fixedChoices.Count > 0)
        {
            return AdministrationError.Rule(NotificationErrorCodes.PreferenceNotConfigurable, [.. fixedChoices]);
        }

        IReadOnlyList<NotificationPreference> stored = await repository.GetPreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
        foreach (NotificationPreferenceChange change in changes)
        {
            NotificationPreference? preference = stored.SingleOrDefault(p => p.EventFamilyCode == change.EventFamilyCode && p.Channel == change.Channel);
            bool? before = preference?.IsEnabled;
            if (before == change.IsEnabled)
            {
                continue;
            }

            if (preference is null)
            {
                preference = new NotificationPreference
                {
                    Id = Guid.CreateVersion7(now),
                    UserId = userId,
                    EventFamilyCode = change.EventFamilyCode,
                    Channel = change.Channel,
                    CreatedAt = now,
                    CreatedBy = userId,
                };
                repository.Add(preference);
            }

            preference.IsEnabled = change.IsEnabled;
            preference.UpdatedAt = now;
            preference.UpdatedBy = userId;
            audit.Stage(NotificationAudit.PreferenceChanged(userId, preference, before));
        }

        // Two saves of the same person's choices at once: the loser's unique key is taken, and it answers as a stale write.
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == NotificationSaveOutcome.Saved
            ? await GetPreferencesAsync(userId, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;
    }

    /// <summary>ADR-004: in-app is always on; a mandatory family (security, critical escalation) cannot be turned off.</summary>
    private static bool IsConfigurable(NotificationEventFamilyEntry family, NotificationChannel channel) =>
        !family.IsMandatory && channel != NotificationChannel.InApp;
}
