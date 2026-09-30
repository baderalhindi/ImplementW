namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// Configuration section <c>Notifications:Delivery</c>, and <c>APP_BASE_URL</c>: how deliveries are retried and how short
/// an SMS must be. The values are the delivery team's (notification-runtime.md F-5); no controlled source states them.
/// </summary>
public sealed class NotificationDeliveryPolicy
{
    public const string Section = "Notifications:Delivery";

    /// <summary>Attempts on one delivery before it is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The wait after a first failed attempt; each later failure doubles it.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// ADR-004's budget: the most segments one SMS may cost. An Arabic segment is 70 characters (UCS-2), 67 when concatenated,
    /// and a deep link with two ids is about 125 characters on its own, so three segments leave about 75 for the Arabic text.
    /// </summary>
    public int SmsMaxSegments { get; set; } = 3;

    /// <summary>
    /// The characters an SMS template reserves for its absolute deep link when its budget is checked at authoring. At
    /// sending, the real link is counted; an SMS over budget then is suppressed, never cut.
    /// </summary>
    public int SmsDeepLinkAllowance { get; set; } = 130;

    /// <summary>
    /// <c>APP_BASE_URL</c>, the application's public origin: an e-mail or SMS carries <c>APP_BASE_URL</c> + the deep link.
    /// Without it neither channel can link to anything, so neither is used.
    /// </summary>
    public Uri? AppBaseUrl { get; set; }

    /// <summary>The absolute form of an SPA route.</summary>
    public string AbsoluteLink(string deepLink)
    {
        ArgumentNullException.ThrowIfNull(deepLink);
        return AppBaseUrl is null
            ? throw new InvalidOperationException("APP_BASE_URL is not configured.")
            : AppBaseUrl.AbsoluteUri.TrimEnd('/') + deepLink;
    }
}
