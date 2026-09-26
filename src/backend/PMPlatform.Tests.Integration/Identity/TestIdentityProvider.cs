using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// An OpenID Connect identity provider hosted in the test process on a loopback port: discovery, keys, an authorization
/// endpoint that authenticates <see cref="NextSubject"/> without a login page, and a token endpoint that checks the
/// client secret, the redirect URI, the single use of the code and the PKCE verifier before issuing an RS256 ID token.
/// It stands in for AHDA's identity provider; the API talks to it over HTTP exactly as it would to the real one.
/// </summary>
public sealed class TestIdentityProvider : IAsyncDisposable
{
    public const string ClientId = "pmplatform-test-client";

    /// <summary>A test value, valid only against this in-process provider.</summary>
    public const string ClientSecret = "test-identity-provider-client-secret";

    public const string CallbackUrl = "http://localhost:5173/auth/callback";

    private static readonly string[] ResponseTypes = ["code"];
    private static readonly string[] SubjectTypes = ["public"];
    private static readonly string[] SigningAlgorithms = ["RS256"];
    private static readonly string[] ChallengeMethods = ["S256"];

    private readonly WebApplication _app;
    private readonly RsaSecurityKey _signingKey = new(RSA.Create(2048)) { KeyId = "test-key-1" };
    private readonly ConcurrentDictionary<string, PendingCode> _codes = new();

    private TestIdentityProvider(WebApplication app)
    {
        _app = app;
    }

    public Uri Authority { get; private set; } = null!;

    /// <summary>The directory subject the next authorization authenticates, as the <c>sub</c> claim.</summary>
    public string NextSubject { get; set; } = TestDirectory.Subject(1);

    /// <summary>When set, the next ID token carries this nonce instead of the one it was asked for.</summary>
    public string? NonceOverride { get; set; }

    /// <summary>When set, ID tokens carry these <c>amr</c> values: how the provider says it authenticated the person (TASK-029).</summary>
    public string[]? AuthenticationMethods { get; set; }

    /// <summary>When set, ID tokens carry this <c>auth_time</c>.</summary>
    public DateTimeOffset? AuthenticatedAt { get; set; }

    public static async Task<TestIdentityProvider> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();

        TestIdentityProvider provider = new(app);
        app.MapGet("/.well-known/openid-configuration", provider.Discovery);
        app.MapGet("/jwks", provider.Keys);
        app.MapGet("/authorize", provider.Authorize);
        app.MapPost("/token", provider.TokenAsync);

        await app.StartAsync();
        string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        provider.Authority = new Uri(address);
        return provider;
    }

    public IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["SSO_OIDC_AUTHORITY"] = Authority.AbsoluteUri,
        ["SSO_OIDC_CLIENT_ID"] = ClientId,
        ["SSO_OIDC_CLIENT_SECRET"] = ClientSecret,
        ["SSO_OIDC_CALLBACK_URL"] = CallbackUrl,
        ["Identity:Sso:RequireHttpsMetadata"] = "false",
    };

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        _signingKey.Rsa.Dispose();
    }

    private string Issuer => Authority.AbsoluteUri.TrimEnd('/');

    private IResult Discovery() => Results.Json(new Dictionary<string, object>
    {
        ["issuer"] = Issuer,
        ["authorization_endpoint"] = $"{Issuer}/authorize",
        ["token_endpoint"] = $"{Issuer}/token",
        ["jwks_uri"] = $"{Issuer}/jwks",
        ["response_types_supported"] = ResponseTypes,
        ["subject_types_supported"] = SubjectTypes,
        ["id_token_signing_alg_values_supported"] = SigningAlgorithms,
        ["code_challenge_methods_supported"] = ChallengeMethods,
    });

    private IResult Keys()
    {
        JsonWebKey key = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(_signingKey.Rsa.ExportParameters(false)) { KeyId = _signingKey.KeyId });
        return Results.Json(new { keys = new[] { new { kty = key.Kty, kid = key.Kid, use = "sig", alg = "RS256", n = key.N, e = key.E } } });
    }

    private IResult Authorize(HttpRequest request)
    {
        string? redirectUri = request.Query["redirect_uri"];
        if (request.Query["client_id"] != ClientId || redirectUri != CallbackUrl || request.Query["response_type"] != "code"
            || request.Query["code_challenge_method"] != "S256")
        {
            return Results.BadRequest();
        }

        string code = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(24));
        _codes[code] = new PendingCode(NextSubject, request.Query["nonce"]!, request.Query["code_challenge"]!, redirectUri);
        return Results.Redirect($"{redirectUri}?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(request.Query["state"]!)}");
    }

    private async Task<IResult> TokenAsync(HttpRequest request)
    {
        string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
        if (request.Headers.Authorization != $"Basic {expected}")
        {
            return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        // A code is redeemable once: it is removed whether or not the rest of the request is valid.
        IFormCollection form = await request.ReadFormAsync();
        if (form["grant_type"] != "authorization_code"
            || !_codes.TryRemove(form["code"].ToString(), out PendingCode? pending)
            || pending.RedirectUri != form["redirect_uri"]
            || pending.CodeChallenge != Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString()))))
        {
            return Results.Json(new { error = "invalid_grant" }, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Json(new { id_token = IdToken(pending), token_type = "Bearer", access_token = "unused-by-the-platform" });
    }

    private string IdToken(PendingCode pending)
    {
        DateTime now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = ClientId,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            Claims = IdTokenClaims(pending),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
        });
    }

    private Dictionary<string, object> IdTokenClaims(PendingCode pending)
    {
        Dictionary<string, object> claims = new() { ["sub"] = pending.Subject, ["nonce"] = NonceOverride ?? pending.Nonce };
        if (AuthenticationMethods is { } methods)
        {
            claims["amr"] = methods;
        }

        if (AuthenticatedAt is { } authenticatedAt)
        {
            claims["auth_time"] = authenticatedAt.ToUnixTimeSeconds();
        }

        return claims;
    }

    private sealed record PendingCode(string Subject, string Nonce, string CodeChallenge, string RedirectUri);
}
