using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications;

internal static class NotificationMapping
{
    public static NotificationSummary ToSummary(NotificationDelivery delivery, NotificationIntent intent) =>
        new(delivery.Id, intent.EventFamilyCode, intent.SourceEventType, delivery.RenderedSubject, delivery.RenderedBody, LanguageCode.Of(delivery.RenderedLanguage),
            intent.DeepLink, delivery.Status, intent.OccurredAt, delivery.SentAt, delivery.ReadAt);

    public static NotificationHistoryItem ToHistoryItem(NotificationDelivery delivery, NotificationIntent intent) =>
        new(delivery.Id, delivery.Channel, intent.EventFamilyCode, intent.SourceEventType, delivery.RenderedSubject, delivery.Status,
            delivery.SuppressionReason, delivery.CreatedAt, delivery.SentAt, delivery.DeliveredAt, delivery.ReadAt);

    public static NotificationTemplateDetail ToDetail(NotificationTemplate template) =>
        new(template.Id, template.EventFamilyCode, template.EventType, template.Channel, template.VersionNo, template.SubjectAr, template.SubjectEn,
            template.BodyAr, template.BodyEn, template.LifecycleState, template.CreatedBy, template.CreatedAt, template.ValidatedByUserId,
            template.ValidatedAt, template.PublishedByUserId, template.PublishedAt, template.RetiredAt);

    public static NotificationTemplateSummary ToSummary(NotificationTemplate template) =>
        new(template.Id, template.EventFamilyCode, template.EventType, template.Channel, template.VersionNo, template.LifecycleState, template.UpdatedAt);

    public static NotificationIntentSummary ToSummary(NotificationIntent intent) =>
        new(intent.Id, intent.SourceModule, intent.SourceEventType, intent.SourceReference, intent.EventFamilyCode, intent.Status, intent.SuppressionReason,
            intent.OccurredAt, intent.ReceivedAt, intent.ScheduledFor, intent.ConditionRevalidatedAt);

    public static NotificationDeliverySummary ToSummary(NotificationDelivery delivery) =>
        new(delivery.Id, delivery.NotificationIntentId, delivery.RecipientUserId, delivery.Channel, delivery.NotificationTemplateId, delivery.Status,
            delivery.SuppressionReason, delivery.AttemptCount, delivery.LastAttemptAt, delivery.NextAttemptAt, delivery.SentAt, delivery.DeliveredAt,
            delivery.ReadAt, delivery.DeadLetteredAt, delivery.SegmentCount, delivery.FailureReason);
}
