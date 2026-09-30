using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>
/// A person's own notifications and preferences (TASK-039, for TASK-040's SCR-150–154 and MOD-070–071). Every operation
/// acts on the caller's own deliveries: another person's is not found (R-47).
/// </summary>
public interface INotificationInbox
{
    /// <summary>In-app notifications that were sent, newest first.</summary>
    public Task<NotificationPage> ListAsync(Guid userId, NotificationInboxQuery query, CancellationToken cancellationToken);

    public Task<UnreadNotificationCount> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    public Task<AdministrationResult<NotificationSummary>> GetAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);

    /// <summary>SENT → READ once; reading a read notification changes nothing.</summary>
    public Task<AdministrationResult<NotificationSummary>> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);

    /// <summary>Every unread in-app notification sent until now becomes READ; later ones arrive unread.</summary>
    public Task<NotificationsMarkedRead> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Every delivery to the caller on every channel, newest first.</summary>
    public Task<NotificationHistoryPage> ListHistoryAsync(Guid userId, NotificationHistoryQuery query, CancellationToken cancellationToken);

    /// <exception cref="MasterDataConfig.Contracts.Resolution.ConfigurationMissingException">No NOTIFICATION_ROUTING is in force.</exception>
    public Task<NotificationPreferenceSet> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>All changes or none. In-app and mandatory families are refused; the next send honours the rest.</summary>
    public Task<AdministrationResult<NotificationPreferenceSet>> UpdatePreferencesAsync(
        Guid userId, IReadOnlyList<NotificationPreferenceChange> changes, CancellationToken cancellationToken);
}
