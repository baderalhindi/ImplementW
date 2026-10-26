using System.Text;
using System.Text.Json.Nodes;
using PMPlatform.Api.Observability;

namespace PMPlatform.Tests.Integration.Observability;

/// <summary>The egress filter's reading of the Sentry envelope format: what it redacts, what it keeps, and what it refuses to pass on.</summary>
public sealed class ApmEnvelopeTests
{
    private const string Header = """{"event_id":"9ec79c33ec9942ab8353589fcb2e04dc","dsn":"https://key@apm.example/1","sent_at":"2026-10-20T10:00:00Z"}""";

    [Fact]
    public void EveryJsonItemIsRedactedAndTheEnvelopeHeaderIsKept()
    {
        string @event = """{"logentry":{"formatted":"sign-in for a.person@example.test failed"},"tags":{"password":"hunter2","correlation_id":"c1"},"timestamp":1760954400.5}""";
        string session = """{"sid":"s1","status":"ok"}""";

        string[] lines = Redact($"{Header}\n{{\"type\":\"event\"}}\n{@event}\n{{\"type\":\"session\"}}\n{session}\n");

        Assert.Equal(Header, lines[0]);
        JsonObject redacted = JsonNode.Parse(lines[2])!.AsObject();
        Assert.Equal("sign-in for [REDACTED] failed", redacted["logentry"]!["formatted"]!.GetValue<string>());
        Assert.Equal("[REDACTED]", redacted["tags"]!["password"]!.GetValue<string>());
        Assert.Equal("c1", redacted["tags"]!["correlation_id"]!.GetValue<string>());
        Assert.Equal(1760954400.5, redacted["timestamp"]!.GetValue<double>());
        Assert.Equal(session, lines[4]);
    }

    [Fact]
    public void ALengthPrefixedItemIsReadByItsLengthAndGetsItsNewLength()
    {
        string payload = "{\"note\":\"token=abc\\nline\"}";
        string[] lines = Redact($"{Header}\n{{\"type\":\"event\",\"length\":{Encoding.UTF8.GetByteCount(payload)}}}\n{payload}\n");

        JsonObject itemHeader = JsonNode.Parse(lines[1])!.AsObject();
        Assert.Equal(Encoding.UTF8.GetByteCount(lines[2]), itemHeader["length"]!.GetValue<int>());
        Assert.Equal("token=[REDACTED]\nline", JsonNode.Parse(lines[2])!["note"]!.GetValue<string>());
    }

    /// <summary>An attachment or any other payload that is not JSON cannot be checked, so it never leaves.</summary>
    [Fact]
    public void AnItemThatIsNotJsonIsDropped()
    {
        string binary = "password=hunter2 raw bytes";
        string[] lines = Redact(
            $"{Header}\n{{\"type\":\"attachment\",\"length\":{binary.Length}}}\n{binary}\n{{\"type\":\"event\"}}\n{{\"level\":\"error\"}}\n");

        Assert.Equal([Header, "{\"type\":\"event\"}", "{\"level\":\"error\"}", string.Empty], lines);
    }

    private static string[] Redact(string envelope) =>
        Encoding.UTF8.GetString(ApmEnvelope.Redact(Encoding.UTF8.GetBytes(envelope))).Split('\n');
}
