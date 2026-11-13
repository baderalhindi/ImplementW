using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PMPlatform.Application.Features.IdentityAccess.Authentication;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>The SMS provider as the tests see it: every message is kept, and the six-digit code can be read back.</summary>
internal sealed partial class TestSmsCodeSender : ISmsCodeSender
{
    public ConcurrentQueue<(string MobileNumber, string Text)> Sent { get; } = new();

    public bool IsConfigured => true;

    public Task SendAsync(string mobileNumber, string text, CancellationToken cancellationToken)
    {
        Sent.Enqueue((mobileNumber, text));
        return Task.CompletedTask;
    }

    /// <summary>The code in the last message to <paramref name="mobileNumber"/>.</summary>
    public string LastCodeTo(string mobileNumber) =>
        Code().Match(Sent.Last(m => m.MobileNumber == mobileNumber).Text).Value;

    [GeneratedRegex("[0-9]{6}")]
    private static partial Regex Code();
}
