using System.Net;
using System.Net.Http.Json;
using PMPlatform.Api.Middleware;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.EndpointHardening;

/// <summary>
/// TASK-078 (CTL-12), acceptance criterion 2 and <c>CORS_ALLOWED_ORIGINS</c>'s verification method: a request from a non-allowlisted
/// Origin is rejected by CORS. The API here allows the one origin an environment has under TASK-016 D-1.
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed class OriginAllowlistTests(IdentityTestHost host)
{
    private const string AppOrigin = "https://pmplatform.ahda.example";

    private static readonly IReadOnlyDictionary<string, string?> AllowlistSettings = HardeningApi.Settings((OriginAllowlist.Variable, AppOrigin));

    /// <summary>A hostile page's preflight is refused outright: 403 in the R-23 envelope, and no Access-Control-Allow-* for the browser to act on.</summary>
    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("https://pmplatform.ahda.example.evil.example")]
    [InlineData("http://pmplatform.ahda.example")]
    [InlineData("https://pmplatform.ahda.example:8443")]
    [InlineData("null")]
    public async Task APreflightFromAnOriginNotListedIsRefused(string origin)
    {
        await using IdentityApiFactory api = host.CreateApi(AllowlistSettings);
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage preflight = await client.SendAsync(Preflight(origin, SessionApi.Sessions));

        Assert.Equal((HttpStatusCode.Forbidden, "ORIGIN_NOT_ALLOWED"), (preflight.StatusCode, await preflight.CodeAsync()));
        Assert.DoesNotContain(preflight.Headers, header => header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A request that needs no preflight — a form-encoded sign-in posted by a hostile page — is refused before it runs, so the page
    /// cannot use visitors' browsers to guess passwords. CORS alone would only hide the answer from the page.
    /// </summary>
    [Fact]
    public async Task ARequestFromAnOriginNotListedIsNotExecuted()
    {
        await using IdentityApiFactory api = host.CreateApi(AllowlistSettings);
        using HttpClient client = api.CreateClient();
        string address = HardeningApi.NewClientAddress();

        using HttpRequestMessage hostile = new HttpRequestMessage(HttpMethod.Post, SessionApi.Sessions)
        { Content = JsonContent.Create(new { username = "local.r02", password = TestDirectory.PersonPassword }) }.From(address);
        hostile.Headers.TryAddWithoutValidation("Origin", "https://evil.example");
        using HttpResponseMessage refused = await client.SendAsync(hostile);

        Assert.Equal((HttpStatusCode.Forbidden, "ORIGIN_NOT_ALLOWED"), (refused.StatusCode, await refused.CodeAsync()));
        Assert.False(refused.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.DoesNotContain("refreshToken", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>The allowed origin's preflight gets the policy: its origin back, the methods and headers the SPA uses, no credentials.</summary>
    [Fact]
    public async Task ThePreflightFromTheAllowedOriginGetsThePolicy()
    {
        await using IdentityApiFactory api = host.CreateApi(AllowlistSettings);
        using HttpClient client = api.CreateClient();

        using HttpResponseMessage preflight = await client.SendAsync(Preflight(AppOrigin, AdministrationApi.Users, "authorization,content-type,idempotency-key,if-match"));

        Assert.Equal(HttpStatusCode.NoContent, preflight.StatusCode);
        Assert.Equal(AppOrigin, Header(preflight, "Access-Control-Allow-Origin"));
        Assert.Equal("GET,POST,PUT,PATCH,DELETE", Header(preflight, "Access-Control-Allow-Methods"));
        Assert.Equal("Authorization,Content-Type,If-Match,Idempotency-Key,X-Correlation-Id", Header(preflight, "Access-Control-Allow-Headers"));
        Assert.Equal("600", Header(preflight, "Access-Control-Max-Age"));
        Assert.False(preflight.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    /// <summary>The SPA's own request — a same-origin sign-in, which browsers send with Origin — runs, and the SPA may read what it needs.</summary>
    [Fact]
    public async Task TheAllowedOriginsRequestRuns()
    {
        await using IdentityApiFactory api = host.CreateApi(AllowlistSettings);
        using HttpClient client = api.CreateClient();

        using HttpRequestMessage signIn = new HttpRequestMessage(HttpMethod.Post, SessionApi.Sessions)
        { Content = JsonContent.Create(new { username = "local.r02", password = "wrong" }) }.From(HardeningApi.NewClientAddress());
        signIn.Headers.TryAddWithoutValidation("Origin", AppOrigin);
        using HttpResponseMessage response = await client.SendAsync(signIn);

        Assert.Equal((HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED"), (response.StatusCode, await response.CodeAsync()));
        Assert.Equal(AppOrigin, Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal("ETag,Location,Retry-After,Content-Disposition,X-Correlation-Id", Header(response, "Access-Control-Expose-Headers"));
    }

    /// <summary>A request without Origin — curl, a server, a same-origin read — is not a cross-origin browser request and is not affected.</summary>
    [Fact]
    public async Task ARequestWithoutOriginIsNotAffected()
    {
        await using IdentityApiFactory api = host.CreateApi(AllowlistSettings);
        using HttpClient client = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.SignInFromAsync(HardeningApi.NewClientAddress(), "local.r02", "wrong").OutcomeAsync()).Status);
    }

    /// <summary>Each entry is an origin as the browser serialises it; anything that would widen the policy stops the API instead.</summary>
    [Theory]
    [InlineData("https://pmplatform.ahda.example", false, "https://pmplatform.ahda.example")]
    [InlineData(" HTTPS://PMPlatform.AHDA.example:443 ", false, "https://pmplatform.ahda.example")]
    [InlineData("https://pmplatform.ahda.example:8443,https://other.ahda.example", false, "https://other.ahda.example https://pmplatform.ahda.example:8443")]
    [InlineData("http://localhost:5173", true, "http://localhost:5173")]
    [InlineData("", false, "")]
    public void TheAllowlistIsParsedIntoOrigins(string value, bool allowHttp, string expected) =>
        Assert.Equal(expected, string.Join(' ', OriginAllowlist.Parse(value, allowHttp).Order(StringComparer.Ordinal)));

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.ahda.example")]
    [InlineData("https://pmplatform.ahda.example/")]
    [InlineData("https://pmplatform.ahda.example/app")]
    [InlineData("https://pmplatform.ahda.example?x=1")]
    [InlineData("https://user@pmplatform.ahda.example")]
    [InlineData("http://pmplatform.ahda.example")]
    [InlineData("ftp://pmplatform.ahda.example")]
    [InlineData("pmplatform.ahda.example")]
    public void AnEntryThatIsNotAnHttpsOriginIsRefused(string value)
    {
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => OriginAllowlist.Parse(value, allowHttp: false));
        Assert.StartsWith($"CORS_ALLOWED_ORIGINS entry '{value}' is not an origin", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A bad entry in the environment's value stops the API at start-up, rather than serving under a policy no one wrote.</summary>
    [Fact]
    public void AnInvalidAllowlistStopsTheApi()
    {
        using IdentityApiFactory api = host.CreateApi(HardeningApi.Settings((OriginAllowlist.Variable, "https://*.ahda.example")));

        Exception failure = Assert.ThrowsAny<Exception>(api.CreateClient);
        Assert.Contains("CORS_ALLOWED_ORIGINS entry 'https://*.ahda.example' is not an origin", failure.ToString(), StringComparison.Ordinal);
    }

    private static HttpRequestMessage Preflight(string origin, string path, string headers = "content-type")
    {
        HttpRequestMessage request = new(HttpMethod.Options, path);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", headers);
        return request;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(',', values) : null;
}
