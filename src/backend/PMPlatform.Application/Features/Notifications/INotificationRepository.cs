using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// The <c>notifications</c> schema (TASK-039). Finds that return rows to change track them; the others do not. A
/// <c>Claim…</c> opens a transaction and locks the row (<c>FOR UPDATE SKIP LOCKED</c>), so a second worker skips it;
/// <see cref="SaveAsync"/> commits it and <see cref="AbandonAsync"/> rolls it back.
/// </summary>
public interface INotificationRepository
{
    public Task<bool> IntentExistsAsync(string sourceEventType, string sourceReference, CancellationToken cancellationToken);

    /// <summary>
    /// The envelope the intent was received in, as committed in <c>common.outbox_message</c> (RETAIN): the scope anchors
    /// and the reminder condition the <c>notification_intent</c> row has no column for (notification-runtime.md F-3).
    /// </summary>
    public Task<NotificationIntentEnvelope?> FindEnvelopeAsync(NotificationIntent intent, CancellationToken cancellationToken);

    /// <summary>RECEIVED intents, and SCHEDULED ones whose time has come, least recently tried first.</summary>
    public Task<IReadOnlyList<Guid>> FindDueIntentIdsAsync(DateTimeOffset now, int count, CancellationToken cancellationToken);

    /// <summary>Locked and tracked, if it is still RECEIVED or SCHEDULED and not locked by another worker.</summary>
    public Task<NotificationIntent?> ClaimIntentAsync(Guid intentId, CancellationToken cancellationToken);

    /// <summary>Moves a due intent a failed pass could not route to the back of the queue.</summary>
    public Task TouchIntentAsync(Guid intentId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<NotificationIntent?> FindIntentAsync(Guid intentId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<NotificationIntentParameter>> GetParametersAsync(Guid intentId, CancellationToken cancellationToken);

    /// <summary>The PUBLISHED template of each channel for the event type, the highest version if there are two. Not tracked.</summary>
    public Task<IReadOnlyDictionary<NotificationChannel, NotificationTemplate>> FindPublishedTemplatesAsync(string eventType, CancellationToken cancellationToken);

    /// <summary>The recipients' choices for the family. Not tracked.</summary>
    public Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(IReadOnlyCollection<Guid> userIds, string eventFamilyCode, CancellationToken cancellationToken);

    /// <summary>PENDING deliveries, and FAILED ones whose next attempt is due, earliest due first.</summary>
    public Task<IReadOnlyList<Guid>> FindDueDeliveryIdsAsync(DateTimeOffset now, int count, CancellationToken cancellationToken);

    /// <summary>Locked and tracked, if it is still PENDING, or FAILED and due, and not locked by another worker.</summary>
    public Task<NotificationDelivery?> ClaimDeliveryAsync(Guid deliveryId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>ROUTED intents none of whose deliveries is PENDING or FAILED become COMPLETED; returns how many.</summary>
    public Task<int> CompleteRoutedIntentsAsync(DateTimeOffset now, int count, CancellationToken cancellationToken);

    /// <summary>The recipient's in-app deliveries that were sent, with their intents, newest first. Not tracked.</summary>
    public Task<(IReadOnlyList<(NotificationDelivery Delivery, NotificationIntent Intent)> Items, int TotalCount)> ListInboxAsync(
        Guid userId, NotificationInboxQuery query, CancellationToken cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The recipient's in-app delivery that was sent, with its intent; tracked.</summary>
    public Task<(NotificationDelivery Delivery, NotificationIntent Intent)?> FindInboxItemAsync(Guid userId, Guid deliveryId, CancellationToken cancellationToken);

    /// <summary>SENT → READ for the recipient's in-app deliveries; returns how many.</summary>
    public Task<int> MarkAllReadAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Every delivery to the recipient, with its intent, newest first. Not tracked.</summary>
    public Task<(IReadOnlyList<(NotificationDelivery Delivery, NotificationIntent Intent)> Items, int TotalCount)> ListHistoryAsync(
        Guid userId, NotificationHistoryQuery query, CancellationToken cancellationToken);

    /// <summary>The user's choices. Tracked.</summary>
    public Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<(IReadOnlyList<NotificationTemplate> Items, int TotalCount)> ListTemplatesAsync(NotificationTemplateQuery query, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<NotificationTemplate?> FindTemplateAsync(Guid templateId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked template, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(NotificationTemplate notificationTemplate);

    /// <summary>The highest version number of the event type and channel; 0 before the first.</summary>
    public Task<int> GetLatestTemplateVersionNoAsync(string eventType, NotificationChannel channel, CancellationToken cancellationToken);

    /// <summary>The PUBLISHED versions of the event type and channel other than <paramref name="exceptTemplateId"/>. Tracked.</summary>
    public Task<IReadOnlyList<NotificationTemplate>> GetPublishedTemplatesAsync(string eventType, NotificationChannel channel, Guid exceptTemplateId, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<(IReadOnlyList<NotificationIntent> Items, int TotalCount)> ListIntentsAsync(NotificationIntentQuery query, CancellationToken cancellationToken);

    /// <summary>Every delivery of the intent, oldest first. Not tracked.</summary>
    public Task<IReadOnlyList<NotificationDelivery>> GetDeliveriesAsync(Guid intentId, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<(IReadOnlyList<NotificationDelivery> Items, int TotalCount)> ListDeliveriesAsync(NotificationDeliveryQuery query, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<NotificationDelivery?> FindDeliveryAsync(Guid deliveryId, CancellationToken cancellationToken);

    public void Add(NotificationIntent intent);

    public void Add(NotificationIntentParameter parameter);

    public void Add(NotificationDelivery delivery);

    public void Add(NotificationTemplate notificationTemplate);

    public void Add(NotificationPreference preference);

    /// <summary>
    /// Saves the tracked changes and commits a claim's transaction. A row changed since it was read, and a unique key another
    /// request took first, are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<NotificationSaveOutcome> SaveAsync(CancellationToken cancellationToken);

    /// <summary>Rolls back a claim's transaction and forgets every tracked change.</summary>
    public Task AbandonAsync();
}
