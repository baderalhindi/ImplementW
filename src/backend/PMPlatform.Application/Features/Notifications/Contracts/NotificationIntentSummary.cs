using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationIntentSummary(
    Guid Id,
    string SourceModule,
    string SourceEventType,
    string SourceReference,
    string EventFamilyCode,
    NotificationIntentStatus Status,
    string? SuppressionReason,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset? ConditionRevalidatedAt);
