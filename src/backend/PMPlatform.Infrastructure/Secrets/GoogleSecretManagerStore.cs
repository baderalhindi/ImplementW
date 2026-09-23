using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PMPlatform.Infrastructure.Secrets;

/// <summary>
/// Google Secret Manager, the approved GCP secret-management service (TASK-019 gate cell, ADR-001).
/// Reads the latest enabled version of a variable over the service's REST access method.
/// </summary>
/// <remarks>
/// <para>
/// A variable's address is <see cref="SecretStoreOptions.Endpoint"/> with the variable's lower-kebab name
/// appended, then the access method: for <c>DB_CONNECTION_STRING</c> under the DEV namespace that is
/// <c>.../secrets/pmplatform-dev-db-connection-string/versions/latest:access</c>. Appending, not URI
/// resolution, is the rule — the configured endpoint deliberately ends in the namespace prefix.
/// </para>
/// <para>
/// Payload residency is a property of the container, not of this call: each secret is created with a
/// user-managed replication policy pinned to the in-Kingdom region (ADR-001 C-5,
/// <c>infra/environments/provision-environment.sh</c>). The control plane is Google's global API; see
/// <c>docs/architecture/secret-management.md</c> §7 F-1.
/// </para>
/// </remarks>
internal sealed class GoogleSecretManagerStore : ISecretStore
{
    private const string AccessMethod = "/versions/latest:access";

    private readonly HttpClient _client;
    private readonly IAccessTokenSource _tokens;
    private readonly Uri _endpoint;

    public GoogleSecretManagerStore(HttpClient client, IAccessTokenSource tokens, SecretStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _tokens = tokens;
        _endpoint = options.Endpoint;
    }

    /// <summary>The address <paramref name="name"/> is read from, under <paramref name="endpoint"/>.</summary>
    public static Uri AddressOf(Uri endpoint, string name)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return new Uri(endpoint.OriginalString + SecretId(name) + AccessMethod);
    }

    /// <summary>The variable's id within its namespace: the sheet's Variable Name, lower-kebab.</summary>
    public static string SecretId(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return name.ToLowerInvariant().Replace('_', '-');
    }

    public async Task<string?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        Uri address = AddressOf(_endpoint, name);
        using HttpRequestMessage request = new(HttpMethod.Get, address);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _tokens.GetAsync(cancellationToken).ConfigureAwait(false));

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new SecretStoreException(Describe(name, address, "could not be reached"), exception);
        }

        using (response)
        {
            // A container that exists but holds no enabled version answers 404 exactly as a missing
            // container does. Both mean the same thing to the application: the value is not set yet.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new SecretStoreException(Describe(
                    name,
                    address,
                    string.Create(CultureInfo.InvariantCulture, $"answered {(int)response.StatusCode}")));
            }

            // The response body carries the secret. It is parsed and handed to the provider here, and is
            // never logged, echoed into an exception message, or written to disk (CTL-18).
            using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("payload", out JsonElement payload)
                || !payload.TryGetProperty("data", out JsonElement data)
                || data.GetString() is not string encoded)
            {
                throw new SecretStoreException(Describe(name, address, "answered a document with no payload.data"));
            }

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch (FormatException exception)
            {
                throw new SecretStoreException(Describe(name, address, "answered a payload that is not base64"), exception);
            }
        }
    }

    private static string Describe(string name, Uri address, string what) =>
        $"{name}: the secret store at {address} {what}.";
}
