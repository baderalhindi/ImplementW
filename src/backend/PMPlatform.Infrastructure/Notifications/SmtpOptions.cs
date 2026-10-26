using System.Globalization;
using MimeKit;

namespace PMPlatform.Infrastructure.Notifications;

/// <summary>
/// AHDA's Exchange SMTP relay (TASK-039): <c>EXCHANGE_SMTP_HOST</c> and <c>EXCHANGE_SMTP_PORT</c> (Public), <c>EXCHANGE_SMTP_USER</c>
/// and <c>EXCHANGE_SMTP_PASSWORD</c> (Secret), and section <c>Notifications:Email</c> for the sender mailbox, which no sheet row
/// names (notification-runtime.md F-6). Without a host the e-mail channel is not used; everything else still runs.
/// </summary>
internal sealed class SmtpOptions
{
    public const string Section = "Notifications:Email";
    public const string HostKey = "EXCHANGE_SMTP_HOST";
    public const string PortKey = "EXCHANGE_SMTP_PORT";

    /// <summary>The relay's submission port with STARTTLS, when the sheet's value is absent.</summary>
    public const int DefaultPort = 587;

    /// <summary>The implicit-TLS port; any other port upgrades with STARTTLS.</summary>
    public const int ImplicitTlsPort = 465;

    public string? Host { get; set; }

    /// <summary>The sheet's value as supplied; <see cref="Port"/> is its number.</summary>
    public string? PortText { get; set; }

    public string? User { get; set; }

    public string? Password { get; set; }

    /// <summary>The From mailbox, e.g. <c>pmplatform-noreply@ahda.gov.sa</c>. Required with a host.</summary>
    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "PMPlatform";

    /// <summary>
    /// True (the default): TLS is required — implicit on port 465, STARTTLS on any other, and a relay that does not offer it
    /// is refused, so the credentials and the content never cross the network in clear. False only for a relay reached on
    /// a private test network.
    /// </summary>
    public bool UseTls { get; set; } = true;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

    public int Port => string.IsNullOrWhiteSpace(PortText) ? DefaultPort : int.Parse(PortText, NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>Without a host there is nothing to check; with one, a port, a sender and credentials that make sense.</summary>
    public bool IsValid() =>
        !IsConfigured
        || ((string.IsNullOrWhiteSpace(PortText) || (int.TryParse(PortText, NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port is > 0 and <= 65535))
            && MailboxAddress.TryParse(FromAddress, out MailboxAddress? from) && from.Address.Contains('@', StringComparison.Ordinal)
            && string.IsNullOrEmpty(User) == string.IsNullOrEmpty(Password)
            && Timeout > TimeSpan.Zero);
}
