using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.Notifications;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>One-time codes go out through the same SMS gateway as notifications (ADR-004), or its test-mailbox stand-in.</summary>
internal sealed class SmsGatewayCodeSender(ISmsGateway sms) : ISmsCodeSender
{
    public bool IsConfigured => sms.IsConfigured;

    public Task SendAsync(string mobileNumber, string text, CancellationToken cancellationToken) => sms.SendAsync(mobileNumber, text, cancellationToken);
}
