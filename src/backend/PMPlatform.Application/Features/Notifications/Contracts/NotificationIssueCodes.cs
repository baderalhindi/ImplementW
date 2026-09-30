namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>Field issue codes of <see cref="NotificationErrorCodes.TemplateInvalid"/>.</summary>
public static class NotificationIssueCodes
{
    /// <summary>A <c>{{</c> or <c>}}</c> that is not a <c>{{name}}</c> placeholder.</summary>
    public const string MalformedPlaceholder = "MALFORMED_PLACEHOLDER";

    /// <summary>An SMS may carry the event and its deep link only (ADR-004): no parameter, and <c>{{deepLink}}</c> required.</summary>
    public const string SmsContentRestricted = "SMS_CONTENT_RESTRICTED";

    /// <summary>The SMS costs more segments than the budget allows (ADR-004).</summary>
    public const string SmsSegmentBudgetExceeded = "SMS_SEGMENT_BUDGET_EXCEEDED";
}
