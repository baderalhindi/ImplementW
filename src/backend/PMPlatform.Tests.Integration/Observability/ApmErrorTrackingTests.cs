using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Api.Observability;
using PMPlatform.Tests.Integration.Identity;
using OpenApiDocument = PMPlatform.Tests.Integration.Project.OpenApiDocument;

namespace PMPlatform.Tests.Integration.Observability;

/// <summary>
/// TASK-090 acceptance criterion 1 in process: "a deliberately thrown unhandled exception in a non-PROD environment appears in the
/// APM dashboard within 5 minutes, correctly attributed to the originating request/correlation ID" — here, at the APM's
/// ingestion endpoint, as sent over the wire. The same against a real APM dashboard is in apm-error-tracking.md §5.3.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class ApmErrorTrackingTests(IdentityTestHost host)
{
    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ADeliberateUnhandledExceptionReachesTheApmAttributedToItsRequest()
    {
        await using ApmIngestStub stub = await ApmIngestStub.StartAsync();
        await using WebApplicationFactory<Program> api = host.CreateApi(stub);
        using HttpClient client = api.CreateClient();
        Session session = await client.SignInOrFailAsync(2);

        Stopwatch elapsed = Stopwatch.StartNew();
        using HttpResponseMessage response = await client.PostAsync($"{ApmApi.DiagnosticExceptions}?page=2", session.AccessToken, new { note = "TASK-090" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        JsonObject problem = await response.ReadObjectAsync();
        Assert.Equal("INTERNAL_ERROR", problem["code"]!.GetValue<string>());
        string correlationId = problem["correlationId"]!.GetValue<string>();
        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-Id").Single());

        JsonObject captured = await stub.WaitForAsync("event", item => ApmIngestStub.Tag(item, ApmEventEnrichment.CorrelationTag) == correlationId, Arrival);
        Assert.True(elapsed.Elapsed < TimeSpan.FromMinutes(5), $"Arrived after {elapsed.Elapsed}.");

        JsonObject exception = captured["exception"]!["values"]!.AsArray().Last()!.AsObject();
        Assert.Equal(typeof(ApmDiagnosticException).FullName, exception["type"]!.GetValue<string>());
        Assert.Contains("TASK-090", exception["value"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(exception["mechanism"]!["handled"]!.GetValue<bool>());
        Assert.Equal(ApmApi.EnvironmentTag, captured["environment"]!.GetValue<string>());
        Assert.Equal("error", captured["level"]!.GetValue<string>());
        Assert.Equal($"POST {ApmApi.DiagnosticExceptions}", captured["transaction"]!.GetValue<string>());

        // Only what the APM needs of the request: no query string, cookies or body, and only the named headers.
        JsonObject request = captured["request"]!.AsObject();
        Assert.Null(request["query_string"]);
        Assert.Null(request["cookies"]);
        Assert.Null(request["data"]);
        Assert.Subset(
            new HashSet<string>(["Accept", "Content-Length", "Content-Type", "User-Agent", "X-Correlation-Id"], StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(request["headers"]!.AsObject().Select(header => header.Key)));

        // The SDK's own installation id is the only user field: never the signed-in person, their name or their address.
        Assert.Equal(["id"], captured["user"]!.AsObject().Select(field => field.Key));
        Assert.NotEqual(session.User.Id.ToString(), captured["user"]!["id"]!.GetValue<string>());
    }

    /// <summary>Latency, throughput and error rate come from a trace per request, tagged like the events and carrying no credential.</summary>
    [Fact]
    public async Task EveryRequestIsTracedWithItsCorrelationId()
    {
        await using ApmIngestStub stub = await ApmIngestStub.StartAsync();
        await using WebApplicationFactory<Program> api = host.CreateApi(stub);
        using HttpClient client = api.CreateClient();
        Session session = await client.SignInOrFailAsync(2);

        using HttpResponseMessage response = await client.GetAsync(SessionApi.Current, session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string correlationId = response.Headers.GetValues("X-Correlation-Id").Single();

        JsonObject trace = await stub.WaitForAsync("transaction", item => ApmIngestStub.Tag(item, ApmEventEnrichment.CorrelationTag) == correlationId, Arrival);
        Assert.Equal($"GET {SessionApi.Current}", trace["transaction"]!.GetValue<string>());
        Assert.True(DateTimeOffset.Parse(trace["timestamp"]!.GetValue<string>(), CultureInfo.InvariantCulture) >= DateTimeOffset.Parse(trace["start_timestamp"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        Assert.Equal("ok", trace["contexts"]!["trace"]!["status"]!.GetValue<string>());
        Assert.DoesNotContain("Bearer", trace.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A failed API call's report lands on the request that failed; any other report on its own request.</summary>
    [Fact]
    public async Task TheSpasErrorReportLandsOnTheRequestThatCausedIt()
    {
        await using ApmIngestStub stub = await ApmIngestStub.StartAsync();
        await using WebApplicationFactory<Program> api = host.CreateApi(stub);
        using HttpClient client = api.CreateClient();

        Guid failedCall = Guid.NewGuid();
        using HttpResponseMessage attributed = await client.PostAsJsonAsync(ApmApi.ClientErrorReports, Report("UNHANDLED_REJECTION", failedCall));
        using HttpResponseMessage unattributed = await client.PostAsJsonAsync(ApmApi.ClientErrorReports, Report("WINDOW_ERROR", correlationId: null));
        Assert.Equal(HttpStatusCode.NoContent, attributed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unattributed.StatusCode);

        JsonObject first = await stub.WaitForAsync("event", item => ApmIngestStub.Tag(item, ApmEventEnrichment.CorrelationTag) == failedCall.ToString(), Arrival);
        Assert.Equal("spa", ApmIngestStub.Tag(first, "origin"));
        Assert.Equal("UNHANDLED_REJECTION", ApmIngestStub.Tag(first, "source"));
        Assert.Equal("/projects/a", first["transaction"]!.GetValue<string>());
        Assert.Equal("TypeError: x is undefined", first["logentry"]!["formatted"]!.GetValue<string>());

        string reportRequest = unattributed.Headers.GetValues("X-Correlation-Id").Single();
        JsonObject second = await stub.WaitForAsync("event", item => ApmIngestStub.Tag(item, ApmEventEnrichment.CorrelationTag) == reportRequest, Arrival);
        Assert.Equal("WINDOW_ERROR", ApmIngestStub.Tag(second, "source"));
    }

    [Theory]
    [InlineData(null, "TypeError", "boom", "/projects", "source REQUIRED")]
    [InlineData("WINDOW_ERROR", "TypeError", "", "/projects", "message REQUIRED")]
    [InlineData("WINDOW_ERROR", "TypeError", "boom", "projects", "path MALFORMED")]
    [InlineData("WINDOW_ERROR", "TypeError", "boom", "/projects?name=x", "path MALFORMED")]
    [InlineData("WINDOW_ERROR", "", "boom", "/projects", "errorType REQUIRED")]
    public async Task AnIncompleteReportIsRefused(string? source, string errorType, string message, string path, string field)
    {
        using HttpClient client = host.Api.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(ApmApi.ClientErrorReports, new { source, errorType, message, stack = (string?)null, path });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, await response.ReadFieldErrorsAsync());
    }

    /// <summary>Without <c>APM_DSN</c> the SDK is not registered and nothing is captured; a report is accepted and dropped.</summary>
    [Fact]
    public async Task WithoutADsnNothingIsRegistered()
    {
        Assert.Null(host.Api.Services.GetService<IHub>());
        using HttpClient client = host.Api.CreateClient();
        Session session = await client.SignInOrFailAsync(2);

        using HttpResponseMessage report = await client.PostAsJsonAsync(ApmApi.ClientErrorReports, Report("WINDOW_ERROR", correlationId: null));
        using HttpResponseMessage diagnostic = await client.PostAsync(ApmApi.DiagnosticExceptions, session.AccessToken, new { note = "off" });

        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, diagnostic.StatusCode);
    }

    /// <summary>The deliberate exception cannot be switched on in production, and a DSN without an environment tag stops the API.</summary>
    [Theory]
    [InlineData("prod", "true")]
    [InlineData("PROD", "true")]
    [InlineData("", "true")]
    [InlineData("", "false")]
    public async Task AnUnsafeApmConfigurationStopsTheApi(string environmentTag, string diagnosticEndpoint)
    {
        await using ApmIngestStub stub = await ApmIngestStub.StartAsync();
        await using WebApplicationFactory<Program> api = host.CreateApi(stub,
            (ApmOptions.EnvironmentTagVariable, environmentTag), ($"{ApmOptions.Section}:DiagnosticExceptionEndpoint", diagnosticEndpoint));

        Exception failure = Assert.ThrowsAny<Exception>(api.CreateClient);
        Assert.Contains($"{ApmOptions.EnvironmentTagVariable} is required with {ApmOptions.DsnVariable}", failure.ToString(), StringComparison.Ordinal);
    }

    /// <summary>The deliberate exception is never part of the contract, even where an environment enables it.</summary>
    [Fact]
    public async Task TheDiagnosticExceptionIsNeverDocumented()
    {
        await using ApmIngestStub stub = await ApmIngestStub.StartAsync();
        await using WebApplicationFactory<Program> api = host.CreateApi(stub);
        using HttpClient client = api.CreateClient();

        string document = (await OpenApiDocument.FetchAsync(client)).Root.ToJsonString();

        Assert.DoesNotContain(ApmApi.DiagnosticExceptions, document, StringComparison.Ordinal);
    }

    private static object Report(string source, Guid? correlationId) => new
    {
        source,
        errorType = "TypeError",
        message = "x is undefined",
        stack = "TypeError: x is undefined\n    at ProjectPage (/assets/index.js:1:2)",
        path = "/projects/a",
        correlationId,
    };
}
