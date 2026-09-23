using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PMPlatform.Infrastructure.Secrets;

/// <summary>Supplies the bearer token the secret store is called with.</summary>
internal interface IAccessTokenSource
{
    public Task<string> GetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The bootstrap credential from <c>SECRET_STORE_AUTH_TOKEN</c>, used where the runtime has no platform
/// identity — a developer machine, or a store AHDA IT nominates in place of GCP's.
/// </summary>
internal sealed class BootstrapTokenSource(string token) : IAccessTokenSource
{
    public Task<string> GetAsync(CancellationToken cancellationToken) => Task.FromResult(token);
}

/// <summary>
/// The runtime service account's token, from the compute platform's metadata server. This is the path
/// Cloud Run takes: the credential is the environment's own identity (TASK-016), it is never written
/// down, and rotating it is Google's business rather than a runbook step.
/// </summary>
internal sealed class MetadataServerTokenSource(HttpClient client) : IAccessTokenSource, IDisposable
{
    private const string TokenPath = "computeMetadata/v1/instance/service-accounts/default/token";

    /// <summary>Renew this long before expiry, so no request is made with a token about to lapse.</summary>
    private static readonly TimeSpan RenewalMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt - RenewalMargin)
            {
                return _token;
            }

            using HttpRequestMessage request = new(HttpMethod.Get, TokenPath);
            request.Headers.Add("Metadata-Flavor", "Google");

            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new SecretStoreException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The metadata server answered {(int)response.StatusCode} for the runtime service account's token; set SECRET_STORE_AUTH_TOKEN where the runtime has no platform identity."));
            }

            MetadataToken issued = await response.Content.ReadFromJsonAsync<MetadataToken>(cancellationToken).ConfigureAwait(false)
                ?? throw new SecretStoreException("The metadata server returned no token document.");

            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(issued.ExpiresIn);
            return _token = issued.AccessToken;
        }
        catch (HttpRequestException exception)
        {
            throw new SecretStoreException("The metadata server could not be reached for the runtime service account's token.", exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private sealed record MetadataToken(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
