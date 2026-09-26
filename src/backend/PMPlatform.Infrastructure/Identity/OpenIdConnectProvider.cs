using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// Single sign-on against AHDA's identity provider (TASK-028, ADR-007): the OpenID Connect authorization code flow
/// with PKCE, the platform as a confidential client. Only the ID token's subject, and its <c>amr</c> and <c>auth_time</c>
/// for TASK-029, are used. No access token is kept, and no group or role claim is read: platform roles are assigned in
/// the platform.
/// </summary>
internal sealed partial class OpenIdConnectProvider(
    IConfiguration configuration,
    IOptions<SingleSignOnOptions> options,
    IHttpClientFactory httpClients,
    SsoTransactionProtector transactions,
    TimeProvider timeProvider,
    ILogger<OpenIdConnectProvider> logger) : ISingleSignOnProvider
{
    public const string HttpClientName = "PMPlatform.Identity.Sso";

    private static readonly JsonWebTokenHandler IdTokenHandler = new() { MapInboundClaims = false };

    private readonly ConcurrentDictionary<Uri, ConfigurationManager<OpenIdConnectConfiguration>> _metadata = new();

    private SingleSignOnOptions Options => options.Value;

    public bool IsConfigured => SingleSignOnClient.Read(configuration) is not null;

    public async Task<SsoAuthorizationResult> BeginAsync(CancellationToken cancellationToken)
    {
        SingleSignOnClient? client = SingleSignOnClient.Read(configuration);
        if (client is null)
        {
            return new SsoAuthorizationResult(null, null, AuthenticationFailure.NotConfigured);
        }

        OpenIdConnectConfiguration? metadata = await MetadataAsync(client, refresh: false, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return new SsoAuthorizationResult(null, null, AuthenticationFailure.ProviderUnavailable);
        }

        SsoTransaction transaction = new(RandomValue(), RandomValue(), RandomValue(), timeProvider.GetUtcNow() + Options.TransactionLifetime);
        string challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(transaction.CodeVerifier)));
        string query = string.Join('&', new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = client.ClientId,
            ["redirect_uri"] = client.CallbackUrl.AbsoluteUri,
            ["scope"] = string.Join(' ', Options.Scopes),
            ["state"] = transaction.State,
            ["nonce"] = transaction.Nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        }.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

        string endpoint = metadata.AuthorizationEndpoint;
        Uri authorizationUrl = new($"{endpoint}{(endpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?')}{query}");
        return new SsoAuthorizationResult(authorizationUrl, transactions.Protect(transaction), null);
    }

    public async Task<SingleSignOnResult> CompleteAsync(string code, string state, string transaction, CancellationToken cancellationToken)
    {
        SingleSignOnClient? client = SingleSignOnClient.Read(configuration);
        if (client is null)
        {
            return SingleSignOnResult.Failed(AuthenticationFailure.NotConfigured);
        }

        // The state proves this browser started this sign-in (CSRF on the redirect); the sealed transaction proves the
        // platform issued it, and carries the nonce and PKCE verifier no one else has seen.
        SsoTransaction? started = string.IsNullOrEmpty(transaction) ? null : transactions.Unprotect(transaction);
        if (started is null
            || started.ExpiresAt <= timeProvider.GetUtcNow()
            || string.IsNullOrEmpty(code)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(started.State), Encoding.UTF8.GetBytes(state ?? string.Empty)))
        {
            return SingleSignOnResult.Failed(AuthenticationFailure.Rejected);
        }

        OpenIdConnectConfiguration? metadata = await MetadataAsync(client, refresh: false, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return SingleSignOnResult.Failed(AuthenticationFailure.ProviderUnavailable);
        }

        (string? idToken, AuthenticationFailure? redeemFailure) = await RedeemAsync(client, metadata, code, started.CodeVerifier, cancellationToken)
            .ConfigureAwait(false);
        if (idToken is null)
        {
            return SingleSignOnResult.Failed(redeemFailure ?? AuthenticationFailure.Rejected);
        }

        TokenValidationResult validated = await ValidateIdTokenAsync(idToken, client, metadata).ConfigureAwait(false);
        if (!validated.IsValid && validated.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // The provider has rotated its signing keys since the metadata was cached.
            metadata = await MetadataAsync(client, refresh: true, cancellationToken).ConfigureAwait(false);
            validated = metadata is null ? validated : await ValidateIdTokenAsync(idToken, client, metadata).ConfigureAwait(false);
        }

        if (!validated.IsValid)
        {
            LogIdTokenRejected(logger, validated.Exception?.GetType().Name ?? "unknown");
            return SingleSignOnResult.Failed(AuthenticationFailure.Rejected);
        }

        if (!validated.Claims.TryGetValue("nonce", out object? nonce) || (nonce as string) != started.Nonce)
        {
            LogIdTokenRejected(logger, "nonce mismatch");
            return SingleSignOnResult.Failed(AuthenticationFailure.Rejected);
        }

        if (!validated.Claims.TryGetValue(Options.SubjectClaim, out object? subject) || subject is not string { Length: > 0 } subjectId)
        {
            return SingleSignOnResult.Failed(AuthenticationFailure.Rejected);
        }

        // TASK-029: how and when the provider authenticated the person. Whether its amr counts as a second factor is the
        // platform's policy (Identity:Mfa:IdentityProviderMethods), not the provider's say.
        IReadOnlyList<string> methods = [.. validated.ClaimsIdentity.FindAll("amr").Select(c => c.Value)];
        DateTimeOffset? authenticatedAt = long.TryParse(validated.ClaimsIdentity.FindFirst("auth_time")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
        return SingleSignOnResult.Authenticated(subjectId, methods, authenticatedAt);
    }

    public SingleSignOnIntegrationStatus Describe() => new(
        IsConfigured,
        SingleSignOnClient.AbsoluteUri(configuration[SingleSignOnClient.AuthorityKey]),
        configuration[SingleSignOnClient.ClientIdKey] is { Length: > 0 } clientId ? clientId : null,
        SingleSignOnClient.AbsoluteUri(configuration[SingleSignOnClient.CallbackUrlKey]),
        !string.IsNullOrEmpty(configuration[ApplicationSecrets.SsoClientSecret]),
        Options.SubjectClaim,
        Options.Scopes);

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        SingleSignOnClient? client = SingleSignOnClient.Read(configuration);
        if (client is null)
        {
            return Result(ConnectionTestOutcome.NotConfigured, failureCode: null);
        }

        try
        {
            // Fetched afresh, not from the cache: the test is of the provider now.
            OpenIdConnectConfiguration metadata = await OpenIdConnectConfigurationRetriever
                .GetAsync(client.MetadataAddress.AbsoluteUri, DocumentRetriever(), cancellationToken)
                .ConfigureAwait(false);
            return IssuerMatches(client, metadata)
                ? Result(ConnectionTestOutcome.Succeeded, failureCode: null)
                : Result(ConnectionTestOutcome.Failed, ConnectionTestResult.IssuerMismatch);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, "connection test", exception.GetType().Name);
            return Result(ConnectionTestOutcome.Failed, ConnectionTestResult.Unreachable);
        }
    }

    /// <summary>The provider's metadata, cached per authority; null if unreachable or if its issuer is not <c>SSO_OIDC_AUTHORITY</c>.</summary>
    private async Task<OpenIdConnectConfiguration?> MetadataAsync(SingleSignOnClient client, bool refresh, CancellationToken cancellationToken)
    {
        ConfigurationManager<OpenIdConnectConfiguration> manager = _metadata.GetOrAdd(
            client.MetadataAddress,
            address => new ConfigurationManager<OpenIdConnectConfiguration>(address.AbsoluteUri, new OpenIdConnectConfigurationRetriever(), DocumentRetriever()));
        if (refresh)
        {
            manager.RequestRefresh();
        }

        try
        {
            OpenIdConnectConfiguration metadata = await manager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            if (IssuerMatches(client, metadata))
            {
                return metadata;
            }

            LogIssuerMismatch(logger);
            return null;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, "metadata", exception.GetType().Name);
            return null;
        }
    }

    /// <summary>Redeems the authorization code at the token endpoint (RFC 6749 §4.1.3, client_secret_basic, RFC 7636 verifier).</summary>
    private async Task<(string? IdToken, AuthenticationFailure? Failure)> RedeemAsync(
        SingleSignOnClient client, OpenIdConnectConfiguration metadata, string code, string codeVerifier, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(metadata.TokenEndpoint, UriKind.Absolute, out Uri? tokenEndpoint)
            || (Options.RequireHttpsMetadata && tokenEndpoint.Scheme != Uri.UriSchemeHttps))
        {
            LogUnavailable(logger, "code redemption", "token endpoint is not an HTTPS URL");
            return (null, AuthenticationFailure.ProviderUnavailable);
        }

        using HttpRequestMessage request = new(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = client.CallbackUrl.AbsoluteUri,
                ["code_verifier"] = codeVerifier,
            }),
        };
        string credentials = $"{Uri.EscapeDataString(client.ClientId)}:{Uri.EscapeDataString(client.ClientSecret)}";
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));

        try
        {
            HttpClient http = httpClients.CreateClient(HttpClientName);
            using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            using JsonDocument body = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return body.RootElement.TryGetProperty("id_token", out JsonElement idToken) && idToken.GetString() is { Length: > 0 } value
                    ? (value, null)
                    : (null, AuthenticationFailure.Rejected);
            }

            string error = body.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("error", out JsonElement e)
                ? e.GetString() ?? string.Empty
                : string.Empty;

            // invalid_grant — a code that is expired, used, or not this client's — concerns the person. Anything else
            // (invalid_client above all: a wrong or rotated SSO_OIDC_CLIENT_SECRET) is the integration failing.
            if (response.StatusCode == HttpStatusCode.BadRequest && error == "invalid_grant")
            {
                return (null, AuthenticationFailure.Rejected);
            }

            LogUnavailable(logger, "code redemption", $"HTTP {(int)response.StatusCode} {error}");
            return (null, AuthenticationFailure.ProviderUnavailable);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, "code redemption", exception.GetType().Name);
            return (null, AuthenticationFailure.ProviderUnavailable);
        }
    }

    private Task<TokenValidationResult> ValidateIdTokenAsync(string idToken, SingleSignOnClient client, OpenIdConnectConfiguration metadata) =>
        IdTokenHandler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = metadata.Issuer,
            ValidAudience = client.ClientId,
            IssuerSigningKeys = metadata.SigningKeys,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                DateTime now = timeProvider.GetUtcNow().UtcDateTime;
                return expires is { } expiry && now < expiry.AddMinutes(1) && (notBefore is null || notBefore <= now.AddMinutes(1));
            },
        });

    private HttpDocumentRetriever DocumentRetriever() =>
        new(httpClients.CreateClient(HttpClientName)) { RequireHttps = Options.RequireHttpsMetadata };

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (content.ConfigureAwait(false))
            {
                return await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}");
        }
    }

    private static bool IssuerMatches(SingleSignOnClient client, OpenIdConnectConfiguration metadata) =>
        string.Equals(metadata.Issuer?.TrimEnd('/'), SingleSignOnClient.IssuerOf(client.Authority), StringComparison.Ordinal);

    private static string RandomValue() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private ConnectionTestResult Result(ConnectionTestOutcome outcome, string? failureCode) => new(outcome, timeProvider.GetUtcNow(), failureCode);

    private static bool IsUnavailable(Exception exception) =>
        exception is HttpRequestException or IOException or TaskCanceledException or TimeoutException or InvalidOperationException or ArgumentException;

    [LoggerMessage(Level = LogLevel.Error, Message = "Identity provider unavailable during {Operation}: {Reason}.")]
    private static partial void LogUnavailable(ILogger logger, string operation, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "The identity provider's issuer does not match SSO_OIDC_AUTHORITY; SSO sign-in is refused.")]
    private static partial void LogIssuerMismatch(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSO sign-in rejected: ID token invalid ({Reason}).")]
    private static partial void LogIdTokenRejected(ILogger logger, string reason);
}
