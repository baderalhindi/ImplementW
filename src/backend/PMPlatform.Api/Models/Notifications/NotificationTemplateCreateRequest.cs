using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.Notifications;

/// <summary>A new template version, created DRAFT. The content rules (bilingual, SMS limits) are the module's.</summary>
public sealed record NotificationTemplateCreateRequest(
    string? EventFamilyCode, string? EventType, NotificationChannel? Channel, string? SubjectAr, string? SubjectEn, string? BodyAr, string? BodyEn)
{
    internal List<FieldError> Validate(out NotificationTemplateDraft? draft)
    {
        List<FieldError> errors = [];
        NotificationTemplateText.Code(EventFamilyCode, "eventFamilyCode", errors);
        NotificationTemplateText.EventType(EventType, "eventType", errors);
        if (Channel is null)
        {
            errors.Add(new FieldError("channel", FieldError.Required));
        }

        NotificationTemplateText.Texts(SubjectAr, SubjectEn, BodyAr, BodyEn, errors);
        draft = errors.Count > 0 ? null : new NotificationTemplateDraft(EventFamilyCode!, EventType!, Channel!.Value, SubjectAr, SubjectEn, BodyAr!, BodyEn!);
        return errors;
    }
}
