using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>An in-app notification as its recipient sees it (SCR-150–152, MOD-070). <see cref="Language"/> is <c>ar</c> or <c>en</c>, the language it was rendered in.</summary>
public sealed record NotificationSummary(
    Guid Id,
    string EventFamilyCode,
    string EventType,
    string? Subject,
    string Body,
    string Language,
    string? DeepLink,
    NotificationDeliveryStatus Status,
    DateTimeOffset OccurredAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? ReadAt);
