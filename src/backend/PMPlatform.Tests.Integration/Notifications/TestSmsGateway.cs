using System.Collections.Concurrent;
using PMPlatform.Application.Features.Notifications;

namespace PMPlatform.Tests.Integration.Notifications;

/// <summary>An SMS provider played by the tests (none is selected, OQ-012): configured, and it records every submission.</summary>
public sealed class TestSmsGateway : ISmsGateway
{
    public bool IsConfigured => true;

    public ConcurrentQueue<(string MobileNumber, string Text)> Sent { get; } = new();

    public Task<string?> SendAsync(string mobileNumber, string text, CancellationToken cancellationToken)
    {
        Sent.Enqueue((mobileNumber, text));
        return Task.FromResult<string?>("test-sms-" + Guid.NewGuid().ToString("N"));
    }
}
