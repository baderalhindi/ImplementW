using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PMPlatform.Application.Common.Observability;

namespace PMPlatform.Tests.Unit.Application.Common;

/// <summary>TASK-090 (CTL-27): the redaction registry and how it is applied to text and JSON.</summary>
public sealed class TelemetryRedactorTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("newPassword")]
    [InlineData("AD_BIND_PASSWORD")]
    [InlineData("SSO_OIDC_CLIENT_SECRET")]
    [InlineData("refresh_token")]
    [InlineData("Authorization")]
    [InlineData("Set-Cookie")]
    [InlineData("DB_CONNECTION_STRING")]
    [InlineData("APM_DSN")]
    [InlineData("national_id")]
    [InlineData("contactEmail")]
    [InlineData("mobile-number")]
    [InlineData("X-Forwarded-For")]
    [InlineData("displayName")]
    public void ARegisteredNameIsSensitiveWhateverItsCaseAndSeparators(string name) => Assert.True(TelemetryRedactor.IsSensitiveName(name));

    [Theory]
    [InlineData("correlation_id")]
    [InlineData("tokenType")]
    [InlineData("code")]
    [InlineData("status")]
    [InlineData("User-Agent")]
    [InlineData("projectId")]
    public void AnOrdinaryNameIsNot(string name) => Assert.False(TelemetryRedactor.IsSensitiveName(name));

    [Theory]
    [InlineData("password=hunter2;Host=db", "password=[REDACTED];Host=db")]
    [InlineData("Host=db;Password=hunter2", "Host=db;Password=[REDACTED]")]
    [InlineData("{\"newPassword\":\"two words\"}", "{\"newPassword\":\"[REDACTED]\"}")]
    [InlineData("national_id: 1098765432 rejected", "national_id: [REDACTED] rejected")]
    [InlineData("Authorization: Bearer abc.def", "Authorization: [REDACTED]")]
    [InlineData("call with eyJhbGciOi.eyJzdWIiOi.c2lnbmF0dXJl failed", "call with [REDACTED] failed")]
    [InlineData("duplicate key (email)=(a.person@example.test)", "duplicate key (email)=([REDACTED])")]
    [InlineData("sms to +966512345678 and 0551234567 queued", "sms to [REDACTED] and [REDACTED] queued")]
    [InlineData("Iqama 2123456789.", "Iqama [REDACTED].")]
    [InlineData("ldap://svc:secret@dc1.example/dc=x", "ldap://[REDACTED]@dc1.example/dc=x")]
    [InlineData("?apiKey=k1&page=2", "?apiKey=[REDACTED]&page=2")]
    public void RegisteredValuesAreReplacedInText(string text, string expected) => Assert.Equal(expected, TelemetryRedactor.Redact(text));

    /// <summary>Identifiers and numbers the APM needs survive: a correlation id, a timestamp inside a word, a longer digit run, a version.</summary>
    [Theory]
    [InlineData("correlation 0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("task TASK-090 at 12345678901 rows")]
    [InlineData("Npgsql 10.0.3, status 500")]
    [InlineData("id a1234567890b")]
    public void OrdinaryTextIsUnchanged(string text) => Assert.Equal(text, TelemetryRedactor.Redact(text));

    [Fact]
    public void JsonLosesRegisteredPropertiesWholeAndRegisteredValuesInStrings()
    {
        JsonNode document = JsonNode.Parse("""
            {"request":{"headers":{"Authorization":"Bearer x","Accept":"application/json"},"cookies":{"a":"b"}},
             "user":{"ip_address":"10.0.0.1","email":"a@b.example"},
             "extra":{"stack":["at f (mailto:a.person@example.test)"],"count":3,"flag":true,"none":null}}
            """)!;

        TelemetryRedactor.Redact(document);

        Assert.Equal(
            """{"request":{"headers":{"Authorization":"[REDACTED]","Accept":"application/json"},"cookies":"[REDACTED]"},"user":{"ip_address":"[REDACTED]","email":"[REDACTED]"},"extra":{"stack":["at f (mailto:[REDACTED])"],"count":3,"flag":true,"none":null}}""",
            document.ToJsonString());
    }

    [Fact]
    public void ATopLevelStringIsRedacted() =>
        Assert.Equal("\"token=[REDACTED]\"", TelemetryRedactor.Redact(JsonValue.Create("token=abc"))!.ToJsonString());

    /// <summary>The registry is well-formed: names as the matcher compares them, patterns that compile, a source for every entry.</summary>
    [Fact]
    public void EveryRegistryEntryIsUsable()
    {
        Assert.All(RedactionRegistry.Fields, field =>
        {
            Assert.NotEmpty(field.Names);
            Assert.All(field.Names, name => Assert.Matches("^[a-z0-9]+$", name));
            Assert.False(string.IsNullOrWhiteSpace(field.Source));
            if (field.ValuePattern is { } pattern)
            {
                _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            }
        });
        Assert.Equal(RedactionRegistry.Fields.Count, RedactionRegistry.Fields.Select(field => field.Field).Distinct().Count());
    }
}
