using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.Notifications.Contracts;

namespace PMPlatform.Api.Models.Notifications;

/// <summary>A DRAFT's full text.</summary>
public sealed record NotificationTemplateUpdateRequest(string? SubjectAr, string? SubjectEn, string? BodyAr, string? BodyEn)
{
    internal List<FieldError> Validate(out NotificationTemplateChanges? changes)
    {
        List<FieldError> errors = [];
        NotificationTemplateText.Texts(SubjectAr, SubjectEn, BodyAr, BodyEn, errors);
        changes = errors.Count > 0 ? null : new NotificationTemplateChanges(SubjectAr, SubjectEn, BodyAr!, BodyEn!);
        return errors;
    }
}
