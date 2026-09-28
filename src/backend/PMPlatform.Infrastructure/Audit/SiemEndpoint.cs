using Microsoft.Extensions.Configuration;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Audit;

/// <summary>
/// <c>SIEM_ENDPOINT_URL</c> (configuration store) and <c>SIEM_API_TOKEN</c> (secret store), read at each use so a rotated
/// token applies without a restart. <see cref="Read"/> returns null unless both are set and the endpoint is HTTPS where
/// HTTPS is required.
/// </summary>
internal sealed record SiemEndpoint(Uri Url, string ApiToken)
{
    public const string UrlKey = "SIEM_ENDPOINT_URL";

    public static SiemEndpoint? Read(IConfiguration configuration, bool requireHttps)
    {
        string? token = configuration[ApplicationSecrets.SiemApiToken];
        return Uri.TryCreate(configuration[UrlKey], UriKind.Absolute, out Uri? url)
               && (url.Scheme == Uri.UriSchemeHttps || (!requireHttps && url.Scheme == Uri.UriSchemeHttp))
               && !string.IsNullOrEmpty(token)
            ? new SiemEndpoint(url, token)
            : null;
    }

    /// <summary>The token never appears in a string form of this record.</summary>
    public override string ToString() => $"{nameof(SiemEndpoint)} {{ Url = {Url} }}";
}
