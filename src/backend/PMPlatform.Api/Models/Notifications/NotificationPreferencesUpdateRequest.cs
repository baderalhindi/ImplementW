using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Features.Notifications.Contracts;

namespace PMPlatform.Api.Models.Notifications;

/// <summary>The caller's choices to set; a family and channel not listed keeps its current choice.</summary>
public sealed record NotificationPreferencesUpdateRequest(IReadOnlyList<NotificationPreferenceItemRequest>? Preferences)
{
    public const int MaxItems = 200;

    internal List<FieldError> Validate(out IReadOnlyList<NotificationPreferenceChange>? changes)
    {
        List<FieldError> errors = [];
        if (Preferences is null)
        {
            errors.Add(new FieldError("preferences", FieldError.Required));
        }
        else if (Preferences.Count > MaxItems)
        {
            errors.Add(new FieldError("preferences", FieldError.MaxLength));
        }
        else
        {
            for (int i = 0; i < Preferences.Count; i++)
            {
                NotificationPreferenceItemRequest? item = Preferences[i];
                RequestValidation.RequireText(item?.EventFamilyCode, $"preferences[{i}].eventFamilyCode", 100, errors);
                if (item?.Channel is null)
                {
                    errors.Add(new FieldError($"preferences[{i}].channel", FieldError.Required));
                }

                if (item?.IsEnabled is null)
                {
                    errors.Add(new FieldError($"preferences[{i}].isEnabled", FieldError.Required));
                }
            }

            if (errors.Count == 0 && Preferences.Select(p => (p.EventFamilyCode, p.Channel)).Distinct().Count() != Preferences.Count)
            {
                errors.Add(new FieldError("preferences", FieldError.NotAllowed));
            }
        }

        changes = errors.Count > 0 ? null : [.. Preferences!.Select(p => new NotificationPreferenceChange(p.EventFamilyCode!, p.Channel!.Value, p.IsEnabled!.Value))];
        return errors;
    }
}
