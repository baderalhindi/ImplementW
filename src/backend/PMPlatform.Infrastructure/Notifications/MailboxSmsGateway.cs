using Microsoft.Extensions.Options;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Notifications;

/// <summary>
/// A stand-in for the SMS provider (13 Nov 2026 review) until AHDA selects one (OQ-012, TASK-103): each SMS goes as an
/// e-mail through the configured SMTP relay to <c>sms+&lt;number&gt;@</c><see cref="SmsStandInOptions.MailboxDomain"/>, where a
/// test mailbox such as mailpit shows it. It is for development and demonstrations only: a real phone receives nothing,
/// and anyone who can read that mailbox can read every code.
/// </summary>
internal sealed class MailboxSmsGateway(IEmailSender email, IOptions<SmsStandInOptions> options) : ISmsGateway
{
    public bool IsConfigured => email.IsConfigured;

    public Task<string?> SendAsync(string mobileNumber, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mobileNumber);
        string digits = new([.. mobileNumber.Where(char.IsAsciiDigit)]);
        return email.SendAsync(new EmailMessage($"sms+{digits}@{options.Value.MailboxDomain}", $"SMS to {mobileNumber}", text, Language.En), cancellationToken);
    }
}

/// <summary><c>Notifications:Sms:StandIn</c>: whether SMS goes to a test mailbox instead of a phone.</summary>
internal sealed class SmsStandInOptions
{
    public const string Section = "Notifications:Sms:StandIn";

    /// <summary>Off by default; on only where no real SMS provider exists and the mailbox is a test one.</summary>
    public bool Enabled { get; set; }

    public string MailboxDomain { get; set; } = "sms.invalid";
}
