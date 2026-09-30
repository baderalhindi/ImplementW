namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>Why an intent or a delivery sent nothing: the values of <c>suppression_reason</c>.</summary>
public static class NotificationSuppressionReasons
{
    /// <summary>Intent: the reminder's subject is no longer in a status that needs it (or no longer exists).</summary>
    public const string ConditionResolved = "CONDITION_RESOLVED";

    /// <summary>Intent: the reminder's source module offers no way to revalidate it, so it is not sent unverified.</summary>
    public const string ConditionUnverifiable = "CONDITION_UNVERIFIABLE";

    /// <summary>Intent: no one holds a recipient role over the intent's scope.</summary>
    public const string NoEligibleRecipient = "NO_ELIGIBLE_RECIPIENT";

    /// <summary>Intent FAILED: no NOTIFICATION_ROUTING in force, or it has no such family.</summary>
    public const string ConfigurationMissing = "CONFIGURATION_MISSING";

    /// <summary>Intent FAILED: a routed channel has no PUBLISHED template for the event type (ADR-012: templates are mandatory).</summary>
    public const string TemplateMissing = "TEMPLATE_MISSING";

    /// <summary>Intent FAILED: a template names a parameter the intent did not supply.</summary>
    public const string TemplateParameterMissing = "TEMPLATE_PARAMETER_MISSING";

    /// <summary>Delivery: the recipient turned this non-mandatory family off on this channel.</summary>
    public const string RecipientOptedOut = "RECIPIENT_OPTED_OUT";

    /// <summary>Delivery: the channel is off by default for the family and the recipient has not turned it on.</summary>
    public const string ChannelOffByDefault = "CHANNEL_OFF_BY_DEFAULT";

    /// <summary>Delivery: the relay or gateway, or <c>APP_BASE_URL</c>, is not configured in this environment.</summary>
    public const string ChannelNotConfigured = "CHANNEL_NOT_CONFIGURED";

    /// <summary>Delivery: SMS, and the recipient has no verified mobile number (ADR-004).</summary>
    public const string MobileUnverified = "MOBILE_UNVERIFIED";

    /// <summary>Delivery: the rendered SMS, with its real link, is over the segment budget.</summary>
    public const string SmsSegmentBudgetExceeded = "SMS_SEGMENT_BUDGET_EXCEEDED";

    /// <summary>Delivery: at the send attempt the recipient no longer held a recipient role over the scope, or was inactive.</summary>
    public const string RecipientIneligible = "RECIPIENT_INELIGIBLE";
}
