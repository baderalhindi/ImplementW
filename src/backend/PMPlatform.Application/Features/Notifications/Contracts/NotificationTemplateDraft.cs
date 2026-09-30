using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>A new template version, created DRAFT with the next version number of its event type and channel.</summary>
public sealed record NotificationTemplateDraft(
    string EventFamilyCode, string EventType, NotificationChannel Channel, string? SubjectAr, string? SubjectEn, string BodyAr, string BodyEn);
