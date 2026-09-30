namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>WF-15's module error codes (api-conventions R-27).</summary>
public static class NotificationErrorCodes
{
    /// <summary>422: the template breaks a content rule; the fields say which (<see cref="NotificationIssueCodes"/>).</summary>
    public const string TemplateInvalid = "NOTIFICATION_TEMPLATE_INVALID";

    /// <summary>422: author, reviewer and publisher of a template must be three people (ERD D-12).</summary>
    public const string SeparationOfDuties = "NOTIFICATION_SEPARATION_OF_DUTIES";

    /// <summary>409: another version of the same event type and channel took the same number first.</summary>
    public const string TemplateVersionConflict = "NOTIFICATION_TEMPLATE_VERSION_CONFLICT";

    /// <summary>422: in-app, or a mandatory family, which a recipient cannot turn off (ADR-004).</summary>
    public const string PreferenceNotConfigurable = "NOTIFICATION_PREFERENCE_NOT_CONFIGURABLE";

    /// <summary>422: a family the routing configuration in force does not have, or a channel it does not route the family to.</summary>
    public const string PreferenceInvalid = "NOTIFICATION_PREFERENCE_INVALID";
}
