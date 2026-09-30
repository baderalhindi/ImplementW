namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>A DRAFT's new text; only its author edits it.</summary>
public sealed record NotificationTemplateChanges(string? SubjectAr, string? SubjectEn, string BodyAr, string BodyEn);
