using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>One delivery to the caller on any channel, with what became of it (SCR-153). The content is in-app's; see <see cref="NotificationSummary"/>.</summary>
public sealed record NotificationHistoryItem(
    Guid Id,
    NotificationChannel Channel,
    string EventFamilyCode,
    string EventType,
    string? Subject,
    NotificationDeliveryStatus Status,
    string? SuppressionReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReadAt);
