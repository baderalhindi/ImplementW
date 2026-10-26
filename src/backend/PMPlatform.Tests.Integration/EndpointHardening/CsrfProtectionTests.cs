using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PMPlatform.Api.Middleware;
using PMPlatform.Tests.Integration.Identity;

namespace PMPlatform.Tests.Integration.EndpointHardening;

/// <summary>
/// TASK-079 (CTL-13): CSRF protection for the browser session. The session is the bearer access token the SPA holds and sends
/// itself, and <see cref="CsrfProtection"/> refuses any state-changing request a page on another origin could have made a browser
/// send without it (csrf-protection.md).
/// </summary>
[Collection(IdentitySuite.Name)]
public sealed partial class CsrfProtectionTests(IdentityTestHost host)
{
    private const string AppOrigin = "https://pmplatform.ahda.example";
    private const string HostileOrigin = "https://evil.example";

    /// <summary>
    /// The acceptance criterion and the workbook's validation check. The captured request is an administrator's "disable this
    /// user" — the request a forger wants most. It is replayed as every kind of request a hostile page can make the
    /// administrator's browser send: none runs, and the user is still active. The same request from the SPA's origin with the
    /// session's token then succeeds.
    /// </summary>
    [Fact]
    public async Task AReplayedStateChangingRequestFromAnotherOriginWithoutTheTokenIsRejected()
    {
        await using IdentityApiFactory api = host.CreateApi(HardeningApi.Settings((OriginAllowlist.Variable, AppOrigin)));
        using HttpClient client = api.CreateClient();
        Session administrator = await client.SignInOrFailAsync(1);
        string target = IdentityDatabase.UserId(8);
        string captured = $"{AdministrationApi.Users}/{target}/disable";
        string userStatus = $"SELECT status FROM identity_access.\"user\" WHERE id = '{target}'";

        try
        {
            (string Replay, Func<HttpRequestMessage> Request, HttpStatusCode Status, string Code)[] replays =
            [
                ("an HTML form posted by the hostile page", () => Replay(captured, HostileOrigin, Form()), HttpStatusCode.Forbidden, "ORIGIN_NOT_ALLOWED"),
                ("the same form with the Origin withheld", () => Replay(captured, origin: null, Form()), HttpStatusCode.Forbidden, "CROSS_SITE_REQUEST_REFUSED"),
                ("a no-cors fetch with a text/plain body", () => Replay(captured, origin: null, new StringContent("{}")), HttpStatusCode.Forbidden, "CROSS_SITE_REQUEST_REFUSED"),
                ("a fetch with credentials the browser attaches itself (Basic)",
                    () => Replay(captured, origin: null, content: null, new AuthenticationHeaderValue("Basic", "bG9jYWwucjAxOng=")), HttpStatusCode.Forbidden, "CROSS_SITE_REQUEST_REFUSED"),
                ("the preflight of a JSON fetch with the token", () => Preflight(captured), HttpStatusCode.Forbidden, "ORIGIN_NOT_ALLOWED"),
                ("the request with a token the hostile page made up",
                    () => Replay(captured, origin: null, content: null, new AuthenticationHeaderValue("Bearer", "forged.token.value")), HttpStatusCode.Unauthorized, "AUTHENTICATION_REQUIRED"),
            ];

            foreach ((string replay, Func<HttpRequestMessage> request, HttpStatusCode expectedStatus, string expectedCode) in replays)
            {
                using HttpRequestMessage sent = request();
                (HttpStatusCode status, string? code) = await client.SendAsync(sent).OutcomeAsync();
                Assert.Equal((replay, expectedStatus, expectedCode), (replay, status, code));
            }

            Assert.Equal(["ACTIVE"], await host.Database.QueryAsync(userStatus));

            using HttpRequestMessage legitimate = Replay(captured, AppOrigin, content: null, new AuthenticationHeaderValue("Bearer", administrator.AccessToken));
            legitimate.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());
            using HttpResponseMessage response = await client.SendAsync(legitimate);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(["DISABLED"], await host.Database.QueryAsync(userStatus));
        }
        finally
        {
            await host.Database.ExecuteAsync($"UPDATE identity_access.\"user\" SET status = 'ACTIVE', disabled_at = NULL WHERE id = '{target}'");
        }
    }

    /// <summary>
    /// "Every state-changing endpoint reachable from a browser session": each controller action with an unsafe method refuses a
    /// forged request — a form post with nothing but what the browser attaches itself — before it is authenticated or routed to
    /// its action. No endpoint is exempt.
    /// </summary>
    [Fact]
    public async Task EveryStateChangingEndpointRefusesAForgedRequest()
    {
        (string Method, string Path)[] writes = [.. host.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
                .Where(method => !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
                .Select(method => (method, "/" + RouteParameter().Replace(endpoint.RoutePattern.RawText!.TrimStart('/'), Guid.NewGuid().ToString()))))];
        using HttpClient client = host.Api.CreateClient();

        List<string> notRefused = [];
        foreach ((string method, string path) in writes)
        {
            using HttpRequestMessage forged = new(new HttpMethod(method), path) { Content = Form() };
            using HttpResponseMessage response = await client.SendAsync(forged);
            if ((response.StatusCode, await response.CodeAsync()) != (HttpStatusCode.Forbidden, "CROSS_SITE_REQUEST_REFUSED"))
            {
                notRefused.Add($"{method} {path}: {(int)response.StatusCode}");
            }
        }

        Assert.True(writes.Length >= 250, $"Only {writes.Length} state-changing endpoints found: the enumeration is broken (252 when TASK-079 was built).");
        Assert.Empty(notRefused);
    }

    /// <summary>
    /// What the rule decides, request shape by request shape. Forgeable: an unsafe method with neither a bearer token nor a body
    /// a browser would preflight. The SPA's own requests — bearer on every authenticated call, JSON on every anonymous one,
    /// multipart uploads with the bearer — are not.
    /// </summary>
    [Theory]
    [InlineData("POST", null, null, true)]
    [InlineData("PUT", null, null, true)]
    [InlineData("PATCH", null, null, true)]
    [InlineData("DELETE", null, null, true)]
    [InlineData("PROPFIND", null, null, true)]
    [InlineData("POST", null, "application/x-www-form-urlencoded", true)]
    [InlineData("POST", null, "multipart/form-data; boundary=x", true)]
    [InlineData("POST", null, "text/plain;charset=UTF-8", true)]
    [InlineData("POST", null, "TEXT/PLAIN", true)]
    [InlineData("POST", null, "not a media type", true)]
    [InlineData("POST", "Basic bG9jYWwucjAxOng=", "application/x-www-form-urlencoded", true)]
    [InlineData("DELETE", "Negotiate YIIB", null, true)]
    [InlineData("POST", "Bearer", null, true)]
    [InlineData("POST", null, "application/json", false)]
    [InlineData("POST", null, "application/json; charset=utf-8", false)]
    [InlineData("POST", null, "application/problem+json", false)]
    [InlineData("POST", "Bearer eyJ.x.y", null, false)]
    [InlineData("DELETE", "bearer eyJ.x.y", null, false)]
    [InlineData("POST", "Bearer eyJ.x.y", "multipart/form-data; boundary=x", false)]
    [InlineData("GET", null, null, false)]
    [InlineData("HEAD", null, null, false)]
    [InlineData("OPTIONS", null, "text/plain", false)]
    public void ARequestIsForgeableUnlessItCarriesTheTokenOrAPreflightedBody(string method, string? authorization, string? contentType, bool forgeable)
    {
        DefaultHttpContext context = new();
        context.Request.Method = method;
        context.Request.ContentType = contentType;
        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        Assert.Equal(forgeable, CsrfProtection.IsForgeable(context.Request));
    }

    /// <summary>
    /// The strategy rests on the session never being ambient: the API authenticates by bearer token alone and sets no cookie, at
    /// sign-in, at the second factor or at refresh. Adding a cookie scheme would need csrf-protection.md revisited first.
    /// </summary>
    [Fact]
    public async Task TheSessionIsNeverACookie()
    {
        IEnumerable<AuthenticationScheme> schemes = await host.Api.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        using HttpClient client = host.Api.CreateClient();

        using HttpResponseMessage mfaPending = await client.SignInAsync("local.r01", TestDirectory.PersonPassword);
        using HttpResponseMessage signedIn = await client.CompleteSecondFactorAsync((await mfaPending.ReadAsync<MfaPending>()).MfaToken);
        using HttpResponseMessage refreshed = await client.RefreshAsync((await signedIn.ReadAsync<Session>()).RefreshToken);

        Assert.Equal(["Bearer"], schemes.Select(scheme => scheme.Name));
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.OK], [mfaPending.StatusCode, signedIn.StatusCode, refreshed.StatusCode]);
        Assert.All([mfaPending, signedIn, refreshed], response => Assert.False(response.Headers.Contains("Set-Cookie")));
    }

    private static HttpRequestMessage Replay(string path, string? origin, HttpContent? content, AuthenticationHeaderValue? authorization = null)
    {
        HttpRequestMessage request = new(HttpMethod.Post, path) { Content = content };
        request.Headers.Authorization = authorization;
        if (origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        return request;
    }

    private static HttpRequestMessage Preflight(string path)
    {
        HttpRequestMessage request = new(HttpMethod.Options, path);
        request.Headers.TryAddWithoutValidation("Origin", HostileOrigin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization,content-type,idempotency-key");
        return request;
    }

    /// <summary>What an HTML form sends: an empty url-encoded body.</summary>
    private static FormUrlEncodedContent Form() => new([]);

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameter();
}
