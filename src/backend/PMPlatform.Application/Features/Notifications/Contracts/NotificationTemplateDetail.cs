using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Notifications.Contracts;

public sealed record NotificationTemplateDetail(
    Guid Id,
    string EventFamilyCode,
    string EventType,
    NotificationChannel Channel,
    int VersionNo,
    string? SubjectAr,
    string? SubjectEn,
    string BodyAr,
    string BodyEn,
    GovernedLifecycleState LifecycleState,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? ValidatedByUserId,
    DateTimeOffset? ValidatedAt,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? RetiredAt);
