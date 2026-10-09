using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// The OpenID Connect authorization code flow with PKCE, the platform as a confidential client (TASK-028): starting an
/// authorization, redeeming its code, and validating the ID token's signature, issuer, audience, lifetime and nonce.
/// What the ID token says is the caller's to read. Single sign-on (<see cref="OpenIdConnectProvider"/>) and Nafath
/// identity verification (<see cref="Nafath.NafathIdentityVerifier"/>, TASK-068) each hold an instance of their own, so
/// their metadata caches and their transaction keys never mix.
/// </summary>
internal sealed partial class OpenIdConnectRelyingParty(
    RelyingPartySettings settings,
    IHttpClientFactory httpClients,
    AuthorizationTransactionProtector transactions,
    TimeProvider timeProvider,
    ILogger logger)
{
    private static readonly JsonWebTokenHandler IdTokenHandler = new() { MapInboundClaims = false };

    private readonly ConcurrentDictionary<Uri, ConfigurationManager<OpenIdConnectConfiguration>> _metadata = new();

    /// <summary>
    /// The provider's authorization URL and the sealed transaction the client returns with the code. <paramref name="binding"/>
    /// is sealed into the transaction and must be presented again to complete it.
    /// </summary>
    public async Task<(Uri? AuthorizationUrl, string? Transaction, AuthenticationFailure? Failure)> BeginAsync(
        OpenIdConnectClient client, string? binding, CancellationToken cancellationToken)
    {
        OpenIdConnectConfiguration? metadata = await MetadataAsync(client, refresh: false, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return (null, null, AuthenticationFailure.ProviderUnavailable);
        }

        AuthorizationTransaction transaction = new(RandomValue(), RandomValue(), RandomValue(), timeProvider.GetUtcNow() + settings.TransactionLifetime, binding);
        string challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(transaction.CodeVerifier)));
        string query = string.Join('&', new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = client.ClientId,
            ["redirect_uri"] = client.CallbackUrl.AbsoluteUri,
            ["scope"] = string.Join(' ', settings.Scopes),
            ["state"] = transaction.State,
            ["nonce"] = transaction.Nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        }.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

        string endpoint = metadata.AuthorizationEndpoint;
        Uri authorizationUrl = new($"{endpoint}{(endpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?')}{query}");
        return (authorizationUrl, transactions.Protect(transaction), null);
    }

    /// <summary>
    /// Redeems the code and validates the ID token; null claims with <see cref="AuthenticationFailure.Rejected"/> when the
    /// code, state, transaction, binding or ID token is not this authorization's, <see cref="AuthenticationFailure.ProviderUnavailable"/>
    /// when the provider cannot be reached or refuses the platform's client.
    /// </summary>
    public async Task<(ValidatedIdToken? IdToken, AuthenticationFailure? Failure)> CompleteAsync(
        OpenIdConnectClient client, string code, string state, string transaction, string? binding, CancellationToken cancellationToken)
    {
        // The state proves this browser started this authorization (CSRF on the redirect); the sealed transaction proves
        // the platform issued it, and carries the nonce and PKCE verifier no one else has seen.
        AuthorizationTransaction? started = string.IsNullOrEmpty(transaction) ? null : transactions.Unprotect(transaction);
        if (started is null
            || started.ExpiresAt <= timeProvider.GetUtcNow()
            || started.Binding != binding
            || string.IsNullOrEmpty(code)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(started.State), Encoding.UTF8.GetBytes(state ?? string.Empty)))
        {
            return (null, AuthenticationFailure.Rejected);
        }

        OpenIdConnectConfiguration? metadata = await MetadataAsync(client, refresh: false, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return (null, AuthenticationFailure.ProviderUnavailable);
        }

        (string? idToken, AuthenticationFailure? redeemFailure) = await RedeemAsync(client, metadata, code, started.CodeVerifier, cancellationToken)
            .ConfigureAwait(false);
        if (idToken is null)
        {
            return (null, redeemFailure ?? AuthenticationFailure.Rejected);
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
            LogIdTokenRejected(logger, settings.ProviderName, validated.Exception?.GetType().Name ?? "unknown");
            return (null, AuthenticationFailure.Rejected);
        }

        if (!validated.Claims.TryGetValue("nonce", out object? nonce) || (nonce as string) != started.Nonce)
        {
            LogIdTokenRejected(logger, settings.ProviderName, "nonce mismatch");
            return (null, AuthenticationFailure.Rejected);
        }

        return (new ValidatedIdToken(validated.Claims, validated.ClaimsIdentity), null);
    }

    /// <summary>Resolves the discovery document afresh and checks its issuer against the configured authority.</summary>
    public async Task<ConnectionTestResult> TestConnectionAsync(OpenIdConnectClient client, CancellationToken cancellationToken)
    {
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
            LogUnavailable(logger, settings.ProviderName, "connection test", exception.GetType().Name);
            return Result(ConnectionTestOutcome.Failed, ConnectionTestResult.Unreachable);
        }
    }

    public ConnectionTestResult Result(ConnectionTestOutcome outcome, string? failureCode) => new(outcome, timeProvider.GetUtcNow(), failureCode);

    /// <summary>The provider's metadata, cached per authority; null if unreachable or if its issuer is not the configured authority.</summary>
    private async Task<OpenIdConnectConfiguration?> MetadataAsync(OpenIdConnectClient client, bool refresh, CancellationToken cancellationToken)
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

            LogIssuerMismatch(logger, settings.ProviderName, settings.AuthorityKey);
            return null;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, settings.ProviderName, "metadata", exception.GetType().Name);
            return null;
        }
    }

    /// <summary>Redeems the authorization code at the token endpoint (RFC 6749 §4.1.3, client_secret_basic, RFC 7636 verifier).</summary>
    private async Task<(string? IdToken, AuthenticationFailure? Failure)> RedeemAsync(
        OpenIdConnectClient client, OpenIdConnectConfiguration metadata, string code, string codeVerifier, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(metadata.TokenEndpoint, UriKind.Absolute, out Uri? tokenEndpoint)
            || (settings.RequireHttpsMetadata && tokenEndpoint.Scheme != Uri.UriSchemeHttps))
        {
            LogUnavailable(logger, settings.ProviderName, "code redemption", "token endpoint is not an HTTPS URL");
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
            HttpClient http = httpClients.CreateClient(settings.HttpClientName);
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
            // (invalid_client above all: a wrong or rotated client secret) is the integration failing.
            if (response.StatusCode == HttpStatusCode.BadRequest && error == "invalid_grant")
            {
                return (null, AuthenticationFailure.Rejected);
            }

            LogUnavailable(logger, settings.ProviderName, "code redemption", $"HTTP {(int)response.StatusCode} {error}");
            return (null, AuthenticationFailure.ProviderUnavailable);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            LogUnavailable(logger, settings.ProviderName, "code redemption", exception.GetType().Name);
            return (null, AuthenticationFailure.ProviderUnavailable);
        }
    }

    private Task<TokenValidationResult> ValidateIdTokenAsync(string idToken, OpenIdConnectClient client, OpenIdConnectConfiguration metadata) =>
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
        new(httpClients.CreateClient(settings.HttpClientName)) { RequireHttps = settings.RequireHttpsMetadata };

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

    private static bool IssuerMatches(OpenIdConnectClient client, OpenIdConnectConfiguration metadata) =>
        string.Equals(metadata.Issuer?.TrimEnd('/'), OpenIdConnectClient.IssuerOf(client.Authority), StringComparison.Ordinal);

    private static string RandomValue() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static bool IsUnavailable(Exception exception) =>
        exception is HttpRequestException or IOException or TaskCanceledException or TimeoutException or InvalidOperationException or ArgumentException;

    [LoggerMessage(Level = LogLevel.Error, Message = "{Provider} unavailable during {Operation}: {Reason}.")]
    private static partial void LogUnavailable(ILogger logger, string provider, string operation, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Provider}'s issuer does not match {AuthorityKey}; its sign-in is refused.")]
    private static partial void LogIssuerMismatch(ILogger logger, string provider, string authorityKey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Provider} ID token rejected ({Reason}).")]
    private static partial void LogIdTokenRejected(ILogger logger, string provider, string reason);
}

/// <summary>
/// What distinguishes one relying party from another: its named HTTP client, its name and authority setting in the log,
/// and its own options.
/// </summary>
internal sealed record RelyingPartySettings(
    string HttpClientName, string ProviderName, string AuthorityKey, IReadOnlyList<string> Scopes, bool RequireHttpsMetadata, TimeSpan TransactionLifetime);

/// <summary>An ID token that passed validation: its claims as the token carries them, and as a claims identity.</summary>
internal sealed record ValidatedIdToken(IDictionary<string, object> Claims, ClaimsIdentity Identity);
