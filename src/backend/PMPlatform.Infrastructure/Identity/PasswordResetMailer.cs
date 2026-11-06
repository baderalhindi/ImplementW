using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// Sends password reset links through the e-mail channel's relay, one at a time from an in-memory queue. They do not go through WF-15: its history stores every
/// rendered body, and a live reset link must not be stored. A restart loses what is queued, and the person asks again. Logs carry the user id and the exception type only.
/// </summary>
internal sealed partial class PasswordResetMailer(
    IEmailSender email,
    NotificationDeliveryPolicy delivery,
    ILogger<PasswordResetMailer> logger) : BackgroundService, IPasswordResetMailer
{
    private const int QueueCapacity = 256;

    private readonly Channel<PasswordResetMail> _queue = Channel.CreateBounded<PasswordResetMail>(
        new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool IsAvailable => email.IsConfigured && delivery.AppBaseUrl is not null;

    public void Enqueue(PasswordResetMail mail) => _queue.Writer.TryWrite(mail);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (PasswordResetMail mail in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await email.SendAsync(Compose(mail), stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // A relay failure must not end the queue, and its message may quote the recipient, so only the type is logged.
            catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogNotSent(logger, mail.UserId, exception.GetType().Name);
            }
        }
    }

    private EmailMessage Compose(PasswordResetMail mail)
    {
        string link = delivery.AbsoluteLink($"/reset-password#token={mail.Link.Token}");
        int minutes = (int)Math.Ceiling((mail.Link.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes);
        return mail.Language == Language.Ar
            ? new EmailMessage(
                mail.To,
                "إعادة تعيين كلمة المرور",
                $"""
                طلب أحدهم إعادة تعيين كلمة مرور حسابك في المنصة. لاختيار كلمة مرور جديدة افتح الرابط التالي:

                {link}

                تنتهي صلاحية الرابط خلال {minutes} دقيقة ولا يعمل إلا مرة واحدة. إذا لم تطلب ذلك فتجاهل هذه الرسالة، وتبقى كلمة مرورك كما هي.
                """,
                mail.Language)
            : new EmailMessage(
                mail.To,
                "Reset your password",
                $"""
                Someone asked to reset the password of your platform account. To choose a new password, open this link:

                {link}

                The link stops working after {minutes} minutes and works once. If you did not ask for this, ignore this message: your password stays as it is.
                """,
                mail.Language);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password reset e-mail for user {UserId} not sent: {ExceptionType}.")]
    private static partial void LogNotSent(ILogger logger, Guid userId, string exceptionType);
}
