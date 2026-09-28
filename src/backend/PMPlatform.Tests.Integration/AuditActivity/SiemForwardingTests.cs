using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.AuditActivity;

/// <summary>
/// TASK-033 acceptance criterion 3 and the second half of its validation check: a failed login reaches the SIEM, end
/// to end, within the forwarding latency (CTL-26). The SIEM is the in-process <see cref="TestSiem"/>; AHDA's SIEM is
/// owed once an environment exists (record F-1).
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class SiemForwardingTests(IdentityTestHost host) : IDisposable
{
    /// <summary>
    /// The bound this test holds forwarding to: the test API polls every 200 ms, so an event normally arrives well under a
    /// second; the rest is room for the backlog of events the suite has already produced.
    /// </summary>
    private static readonly TimeSpan ForwardingLatency = TimeSpan.FromSeconds(10);

    public void Dispose() => host.Siem.Refusing = false;

    [Fact]
    public async Task AFailedSignInReachesTheSiem()
    {
        await using IdentityApiFactory api = host.CreateApi(host.Siem.Settings);
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = api.CreateClient().WithCorrelationId(correlationId);

        Stopwatch latency = Stopwatch.StartNew();
        (await client.SignInAsync("local.r02", "a-wrong-password")).Dispose();
        JsonElement? received = await host.Siem.WaitForEventAsync(correlationId, "IdentityAccess.SignInFailed", ForwardingLatency);
        latency.Stop();

        Assert.True(received is not null, $"The failed sign-in did not reach the SIEM within {ForwardingLatency}.");
        JsonElement siemEvent = received.Value;
        Assert.Equal("AUTHENTICATION", siemEvent.GetProperty("eventClass").GetString());
        Assert.Equal("FAILED", siemEvent.GetProperty("outcome").GetString());
        Assert.Equal("USER", siemEvent.GetProperty("actor").GetProperty("actorType").GetString());
        Assert.Equal(JsonValueKind.Null, siemEvent.GetProperty("actor").GetProperty("userId").ValueKind);
        Dictionary<string, string?> attributes = siemEvent.GetProperty("attributes").EnumerateArray()
            .ToDictionary(a => a.GetProperty("name").GetString()!, a => a.GetProperty("newValue").GetString());
        Assert.Equal("CREDENTIALS_REJECTED", attributes["failure_reason"]);
        Assert.Equal("local.r02", attributes["username"]);
        Assert.Equal("DIRECTORY", attributes["authentication_method"]);

        // What the SIEM holds is what the audit store holds, hash included, and the store records the delivery.
        Guid eventId = siemEvent.GetProperty("eventId").GetGuid();
        Assert.Equal(
            [$"{siemEvent.GetProperty("eventHash").GetString()}|FORWARDED|true"],
            await host.Database.QueryAsync($"""
                SELECT e.event_hash || '|' || f.status || '|' || (f.forwarded_at IS NOT NULL)::text
                FROM audit_activity.audit_event e JOIN audit_activity.audit_forwarding_record f ON f.audit_event_id = e.id
                WHERE e.id = '{eventId}'
                """));
        Assert.True(latency.Elapsed < ForwardingLatency, $"Forwarded in {latency.Elapsed}.");
    }

    /// <summary>A SIEM that refuses leaves the event FAILED, and it is sent again, not lost, once the SIEM accepts.</summary>
    [Fact]
    public async Task AnEventTheSiemRefusedIsSentAgain()
    {
        await using IdentityApiFactory api = host.CreateApi(host.Siem.Settings);
        Guid correlationId = Guid.NewGuid();
        using HttpClient client = api.CreateClient().WithCorrelationId(correlationId);
        host.Siem.Refusing = true;

        (await client.SignInAsync("local.r03", "a-wrong-password")).Dispose();
        string status = $"""
            SELECT f.status FROM audit_activity.audit_forwarding_record f JOIN audit_activity.audit_event e ON e.id = f.audit_event_id
            WHERE e.correlation_id = '{correlationId}'
            """;
        Assert.True(await WaitUntilAsync(async () => (await host.Database.QueryAsync(status)).SequenceEqual(["FAILED"])), "The refused event was not marked FAILED.");

        host.Siem.Refusing = false;

        Assert.NotNull(await host.Siem.WaitForEventAsync(correlationId, "IdentityAccess.SignInFailed", ForwardingLatency));
        Assert.True(await WaitUntilAsync(async () => (await host.Database.QueryAsync(status)).SequenceEqual(["FORWARDED"])), "The event was not marked FORWARDED.");
    }

    /// <summary>CTL-26: the forwarded subset is AHDA's to set, but it cannot leave out authentication.</summary>
    [Fact]
    public async Task ASubsetWithoutAuthenticationStopsStartUp()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Audit:Siem:ForwardedClasses:0"] = "PRIVILEGED_ACTION" });

        OptionsValidationException error = Assert.Throws<OptionsValidationException>(api.CreateClient);
        Assert.Contains("must include AUTHENTICATION (CTL-26)", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A class name with a typing error would forward nothing for the class it meant; it stops start-up instead.</summary>
    [Fact]
    public async Task AnUnknownClassStopsStartUp()
    {
        await using IdentityApiFactory api = host.CreateApi(new Dictionary<string, string?> { ["Audit:Siem:ForwardedClasses:4"] = "AUTHENTICATON" });

        OptionsValidationException error = Assert.Throws<OptionsValidationException>(api.CreateClient);
        Assert.Contains("names a class that is not an audit class", error.Message, StringComparison.Ordinal);
    }

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + ForwardingLatency;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        return false;
    }
}
