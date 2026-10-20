using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Correlation;
using PMPlatform.Application.Common.Observability;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.Observability;

/// <summary>
/// TASK-090 acceptance criterion 2, the redaction cross-check: "no sensitive field appears in captured error payloads
/// (cross-checked against the redaction registry)". Every field of <see cref="RedactionRegistry"/> is planted, under each of its
/// names and, where it has one, in its own shape, through every way a value reaches the APM — the request line, headers and body
/// of a request that throws, the exception's message, a logged error's properties, and the SPA's error report. The test then reads
/// what arrived at the APM endpoint and finds none of them, while a control value planted beside them did arrive.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class ApmRedactionTests(IdentityTestHost host)
{
    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task NoRegisteredFieldReachesTheApm()
    {
        await using ApmIngestStub stub = await ApmIngestStub.StartAsync();
        await using WebApplicationFactory<Program> api = host.CreateApi(stub);
        using HttpClient client = api.CreateClient();
        Session session = await client.SignInOrFailAsync(2);

        string control = $"control{Hex()}";
        (string Name, string Marker)[] named = [.. RedactionRegistry.Fields.SelectMany(field => field.Names).Select(name => (name, $"Mk{Hex()}"))];
        string[] shaped = [.. RedactionRegistry.Fields.Where(field => field.ValuePattern is not null).Select(ShapedMarker)];
        string text = $"{control} {string.Join("; ", named.Select(pair => $"{pair.Name}={pair.Marker}"))} {string.Join(' ', shaped)}";

        // 1. A request that throws, with every marker in its query string, its headers and (through its body) the exception message.
        string query = string.Join('&', named.Select(pair => $"{pair.Name}={pair.Marker}"));
        using HttpRequestMessage throwing = new(HttpMethod.Post, $"{ApmApi.DiagnosticExceptions}?{query}") { Content = JsonContent.Create(new { note = text }) };
        throwing.Headers.Authorization = new("Bearer", session.AccessToken);
        foreach ((string name, string marker) in named)
        {
            throwing.Headers.TryAddWithoutValidation($"X-{name}", marker);
        }

        using HttpResponseMessage thrown = await client.SendAsync(throwing);
        Assert.Equal(HttpStatusCode.InternalServerError, thrown.StatusCode);
        Guid requestCorrelation = Guid.Parse((await thrown.ReadObjectAsync())["correlationId"]!.GetValue<string>());

        // 2. A logged error whose properties carry the markers under their registered names, as a careless log line would.
        Guid loggedCorrelation = Guid.NewGuid();
        ILogger logger = api.Services.GetRequiredService<ILogger<ApmRedactionTests>>();
        using (CorrelationScope.Begin(loggedCorrelation))
        {
            string template = $"Cross-check {{Control}} {string.Join(' ', named.Select(pair => $"{{{pair.Name}}}"))}";
#pragma warning disable CA2254, CA1848 // The template is built from the registry on purpose.
            logger.LogError(template, [control, .. named.Select(pair => (object)pair.Marker)]);
#pragma warning restore CA2254, CA1848
        }

        // 3. The SPA's error report, every marker in its message and stack and a shaped one in its path.
        Guid reportedCorrelation = Guid.NewGuid();
        using HttpResponseMessage reported = await client.PostAsJsonAsync(ApmApi.ClientErrorReports, new
        {
            source = "RENDER_ERROR",
            errorType = "TypeError",
            message = text,
            stack = text,
            path = $"/projects/{shaped[0]}",
            correlationId = reportedCorrelation,
        });
        Assert.Equal(HttpStatusCode.NoContent, reported.StatusCode);

        JsonObject[] arrived =
        [
            await stub.WaitForAsync("event", item => ApmIngestStub.Tag(item, "correlation_id") == requestCorrelation.ToString(), Arrival),
            await stub.WaitForAsync("event", item => ApmIngestStub.Tag(item, "correlation_id") == loggedCorrelation.ToString(), Arrival),
            await stub.WaitForAsync("event", item => ApmIngestStub.Tag(item, "correlation_id") == reportedCorrelation.ToString(), Arrival),
        ];

        Assert.All(arrived, item => Assert.Contains(control, item.ToJsonString(), StringComparison.Ordinal));
        string everything = string.Join('\n', stub.Envelopes);
        Assert.All(named, pair => Assert.DoesNotContain(pair.Marker, everything, StringComparison.Ordinal));
        Assert.All(shaped, marker => Assert.DoesNotContain(marker, everything, StringComparison.Ordinal));
        Assert.DoesNotContain(session.AccessToken, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(session.RefreshToken, everything, StringComparison.Ordinal);
    }

    /// <summary>A value in the field's own shape, so only its value pattern can find it. A registry entry with a pattern and no marker here fails the test.</summary>
    private static string ShapedMarker(RedactedField field) => field.Names[0] switch
    {
        "token" => $"eyJ{Hex()}.eyJ{Hex()}.{Hex()}",
        "authorization" => $"Bearer Mk{Hex()}",
        "dsn" => $"https://mkuser{Hex()}:mkpass{Hex()}@ingest.example.test/1",
        "nationalid" => $"1{Digits(9)}",
        "email" => $"mk{Hex()}@example.test",
        "mobile" => $"+9665{Digits(8)}",
        _ => throw new InvalidOperationException($"The registry's '{field.Field}' has a value pattern but the cross-check plants no value in its shape."),
    };

    private static string Hex() => RandomNumberGenerator.GetHexString(12, lowercase: true);

    private static string Digits(int count) => string.Concat(Enumerable.Range(0, count).Select(_ => RandomNumberGenerator.GetInt32(10)));
}
