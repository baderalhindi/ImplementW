using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PMPlatform.Tests.Integration.Observability;

/// <summary>
/// A Sentry-protocol ingestion endpoint on a loopback port that keeps every envelope exactly as it arrived, so a test sees what
/// the API would have sent to the APM over the wire, after redaction.
/// </summary>
internal sealed class ApmIngestStub : IAsyncDisposable
{
    private const string PublicKey = "apmteststubpublickey";

    private readonly WebApplication _app;
    private readonly ConcurrentQueue<string> _envelopes = new();

    private ApmIngestStub(WebApplication app)
    {
        _app = app;
    }

    public string Dsn { get; private set; } = string.Empty;

    /// <summary>Every envelope received so far, as UTF-8 text.</summary>
    public IReadOnlyList<string> Envelopes => [.. _envelopes];

    public static async Task<ApmIngestStub> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        WebApplication app = builder.Build();
        ApmIngestStub stub = new(app);
        app.MapPost("/api/{project}/envelope/", async (HttpRequest request) =>
        {
            using StreamReader reader = new(request.Body, Encoding.UTF8);
            stub._envelopes.Enqueue(await reader.ReadToEndAsync());
            return Results.Json(new { id = Guid.NewGuid().ToString("N") });
        });
        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        stub.Dsn = $"{address.Replace("http://", $"http://{PublicKey}@", StringComparison.Ordinal)}/1";
        return stub;
    }

    /// <summary>Every item payload of type <paramref name="type"/> (<c>event</c>, <c>transaction</c>) received so far.</summary>
    public IEnumerable<JsonObject> Items(string type)
    {
        foreach (string envelope in Envelopes)
        {
            string[] lines = envelope.Split('\n');
            for (int i = 1; i + 1 < lines.Length; i += 2)
            {
                if (JsonNode.Parse(lines[i])?["type"]?.GetValue<string>() == type && JsonNode.Parse(lines[i + 1]) is JsonObject payload)
                {
                    yield return payload;
                }
            }
        }
    }

    /// <summary>The first item of <paramref name="type"/> that <paramref name="match"/> accepts, waiting for it up to <paramref name="timeout"/>.</summary>
    public async Task<JsonObject> WaitForAsync(string type, Func<JsonObject, bool> match, TimeSpan timeout)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (waited.Elapsed < timeout)
        {
            if (Items(type).FirstOrDefault(match) is { } item)
            {
                return item;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException($"No {type} arrived at the APM stub within {timeout}; {_envelopes.Count} envelopes received.");
    }

    public static string? Tag(JsonObject item, string name) => item["tags"]?[name]?.GetValue<string>();

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
