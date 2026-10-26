using System.Net;
using PMPlatform.Api.Middleware;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.EndpointHardening;

/// <summary>
/// TASK-078 (CTL-12), acceptance criterion 3 in process: every response carries the mandatory headers — successes, refusals the
/// platform writes itself, the framework's 404 and the hardening middleware's own 403 and 429. The scan of the running stack is
/// in endpoint-hardening.md §5.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class SecurityHeadersTests(IdentityTestHost host)
{
    private static readonly string[] Mandatory =
        ["Content-Security-Policy", "X-Content-Type-Options", "X-Frame-Options", "Strict-Transport-Security", "Referrer-Policy"];

    [Fact]
    public async Task EveryKindOfResponseCarriesEveryHeader()
    {
        await using IdentityApiFactory api = host.CreateApi(HardeningApi.Settings(
            (OriginAllowlist.Variable, "https://pmplatform.ahda.example"), ("RateLimiting:Policies:SignIn:PermitLimit", "1")));
        using HttpClient client = api.CreateClient();
        Session session = await client.SignInOrFailAsync(2);
        string address = HardeningApi.NewClientAddress();

        using HttpRequestMessage foreign = new(HttpMethod.Get, "/health");
        foreign.Headers.TryAddWithoutValidation("Origin", "https://evil.example");
        await client.SignInFromAsync(address, "local.r02", "wrong").OutcomeAsync();

        (string Kind, HttpResponseMessage Response)[] responses =
        [
            ("200 health", await client.GetAsync("/health")),
            ("200 read", await client.GetAsync(SessionApi.Current, session.AccessToken)),
            ("401", await client.GetAsync(SessionApi.Current)),
            ("404", await client.GetAsync("/api/v1/no-such-resource", session.AccessToken)),
            ("403 origin", await client.SendAsync(foreign)),
            ("429", await client.SignInFromAsync(address, "local.r02", "wrong")),
        ];

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests],
            responses.Select(response => response.Response.StatusCode));
        foreach ((string kind, HttpResponseMessage response) in responses)
        {
            Assert.All(SecurityHeaders.Headers, header =>
                Assert.True(response.Headers.TryGetValues(header.Key, out IEnumerable<string>? values) && values.Single() == header.Value,
                    $"{kind}: {header.Key} missing or not '{header.Value}'."));
            Assert.False(response.Headers.Contains("Server"), $"{kind}: the Server header names the server.");
            response.Dispose();
        }
    }

    /// <summary>The five the workbook names, with values a scanner grades as passing — the list above is not allowed to drift from them.</summary>
    [Fact]
    public void TheWorkbooksFiveHeadersAreSetToPassingValues()
    {
        Dictionary<string, string> headers = SecurityHeaders.Headers.ToDictionary(header => header.Key, header => header.Value.ToString());

        Assert.All(Mandatory, name => Assert.True(headers.ContainsKey(name), name));
        Assert.Contains("frame-ancestors 'none'", headers["Content-Security-Policy"], StringComparison.Ordinal);
        Assert.StartsWith("default-src 'none'", headers["Content-Security-Policy"], StringComparison.Ordinal);
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("max-age=31536000; includeSubDomains", headers["Strict-Transport-Security"]);
        Assert.Equal("no-referrer", headers["Referrer-Policy"]);
    }
}
