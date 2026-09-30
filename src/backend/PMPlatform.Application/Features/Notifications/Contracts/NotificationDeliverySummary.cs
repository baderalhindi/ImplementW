using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationDeliverySummary(
    Guid Id,
    Guid NotificationIntentId,
    Guid RecipientUserId,
    NotificationChannel Channel,
    Guid NotificationTemplateId,
    NotificationDeliveryStatus Status,
    string? SuppressionReason,
    int AttemptCount,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReadAt,
    DateTimeOffset? DeadLetteredAt,
    short? SegmentCount,
    string? FailureReason);
