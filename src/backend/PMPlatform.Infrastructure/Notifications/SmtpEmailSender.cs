using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using MimeKit.Utils;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Notifications;

/// <summary>
/// The e-mail channel over AHDA's Exchange SMTP relay: one connection per message, plain text in UTF-8, the recipient's
/// language declared. A relay that refuses the connection, the credentials or the message throws, and the delivery is
/// retried or dead-lettered by the sending pass; nothing here can reach the source's transaction.
/// </summary>
internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        SmtpOptions smtp = options.Value;
        if (smtp.Host is not { Length: > 0 } host || smtp.FromAddress is not { } fromAddress)
        {
            throw new InvalidOperationException($"{SmtpOptions.HostKey} is not configured.");
        }

        MailboxAddress from = new(smtp.FromName, fromAddress);
        using MimeMessage mime = new();
        mime.From.Add(from);
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.MessageId = MimeUtils.GenerateMessageId(from.Domain);
        mime.Headers.Add(HeaderId.ContentLanguage, LanguageCode.Of(message.Language));
        mime.Body = new TextPart(TextFormat.Plain) { Text = message.Body };

        using SmtpClient client = new() { Timeout = (int)smtp.Timeout.TotalMilliseconds };
        SecureSocketOptions security = !smtp.UseTls ? SecureSocketOptions.None
            : smtp.Port == SmtpOptions.ImplicitTlsPort ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
        await client.ConnectAsync(host, smtp.Port, security, cancellationToken).ConfigureAwait(false);
        if (smtp.User is { Length: > 0 } user)
        {
            await client.AuthenticateAsync(user, smtp.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
        return mime.MessageId;
    }
}
