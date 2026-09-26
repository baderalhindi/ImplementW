using Microsoft.Extensions.Configuration;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// <c>MFA_PROVIDER_ENDPOINT</c> (configuration store) and <c>MFA_PROVIDER_API_KEY</c> (secret store), read at each use so
/// a rotated key applies without a restart. <see cref="Read"/> returns null unless both are set and the endpoint is
/// HTTPS where HTTPS is required: a second-factor code must not cross the network in clear.
/// </summary>
internal sealed record MultiFactorProviderClient(Uri Endpoint, string ApiKey)
{
    public const string EndpointKey = "MFA_PROVIDER_ENDPOINT";

    public static MultiFactorProviderClient? Read(IConfiguration configuration, bool requireHttps)
    {
        string? apiKey = configuration[ApplicationSecrets.MultiFactorProviderApiKey];
        return Uri.TryCreate(configuration[EndpointKey], UriKind.Absolute, out Uri? endpoint)
               && (endpoint.Scheme == Uri.UriSchemeHttps || (!requireHttps && endpoint.Scheme == Uri.UriSchemeHttp))
               && !string.IsNullOrEmpty(apiKey)
            ? new MultiFactorProviderClient(new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/"), apiKey)
            : null;
    }

    /// <summary>The API key never appears in a string form of this record.</summary>
    public override string ToString() => $"{nameof(MultiFactorProviderClient)} {{ Endpoint = {Endpoint} }}";
}
