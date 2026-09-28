using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// A SIEM ingestion endpoint hosted in the test process on a loopback port, speaking the platform's SIEM contract
/// (auth-access-audit-logging.md §4): it accepts a POST of one event with the right bearer token and keeps what it was
/// sent. <see cref="Refusing"/> makes it answer 503, as a SIEM that is down would.
/// </summary>
public sealed class TestSiem : IAsyncDisposable
{
    /// <summary>A test value, valid only against this in-process SIEM.</summary>
    public const string ApiToken = "test-siem-api-token-7c1e";

    private readonly WebApplication _app;
    private readonly ConcurrentQueue<ReceivedRequest> _requests = new();

    private TestSiem(WebApplication app)
    {
        _app = app;
    }

    public Uri Endpoint { get; private set; } = null!;

    public bool Refusing { get; set; }

    /// <summary>Every request that reached the endpoint, accepted or not.</summary>
    public IEnumerable<ReceivedRequest> Requests => _requests;

    /// <summary>The events the SIEM accepted.</summary>
    public IEnumerable<JsonElement> Events => _requests.Where(r => r.Accepted).Select(r => JsonDocument.Parse(r.Body).RootElement.Clone());

    /// <summary>The API's settings for this SIEM, forwarding every 200 ms so a test waits little.</summary>
    public IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["SIEM_ENDPOINT_URL"] = new Uri(Endpoint, "events").AbsoluteUri,
        ["SIEM_API_TOKEN"] = ApiToken,
        ["Audit:Siem:RequireHttps"] = "false",
        ["Audit:Siem:PollInterval"] = "00:00:00.200",
    };

    public static async Task<TestSiem> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();

        TestSiem siem = new(app);
        app.MapPost("/events", siem.ReceiveAsync);

        await app.StartAsync();
        siem.Endpoint = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First());
        return siem;
    }

    /// <summary>The accepted event of the request <paramref name="correlationId"/> of type <paramref name="eventType"/>, waiting up to <paramref name="within"/>.</summary>
    public async Task<JsonElement?> WaitForEventAsync(Guid correlationId, string eventType, TimeSpan within)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + within;
        do
        {
            JsonElement? found = Events.Cast<JsonElement?>().FirstOrDefault(e =>
                e!.Value.GetProperty("correlationId").GetGuid() == correlationId && e.Value.GetProperty("eventType").GetString() == eventType);
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
        while (DateTimeOffset.UtcNow < deadline);

        return null;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private async Task ReceiveAsync(HttpContext context)
    {
        using StreamReader reader = new(context.Request.Body);
        string body = await reader.ReadToEndAsync();
        bool authorized = context.Request.Headers.Authorization == $"Bearer {ApiToken}";
        bool accepted = authorized && !Refusing;
        _requests.Enqueue(new ReceivedRequest(context.Request.Headers.Authorization.ToString(), body, accepted, DateTimeOffset.UtcNow));
        context.Response.StatusCode = !authorized ? StatusCodes.Status401Unauthorized
            : Refusing ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status202Accepted;
    }

    public sealed record ReceivedRequest(string Authorization, string Body, bool Accepted, DateTimeOffset ReceivedAt);
}
