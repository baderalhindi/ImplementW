using Microsoft.Extensions.Configuration;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity.Nafath;

/// <summary>The platform's client registration at Nafath (<c>NAFATH_*</c>, TASK-068), read at each use.</summary>
internal static class NafathClient
{
    public const string CallbackUrlKey = "NAFATH_CALLBACK_URL";

    /// <summary>
    /// The application identifier Nafath registers the platform under. The OpenID Connect exchange has no place for it,
    /// so it is reported on the status for AHDA IT to match against the registration and is not sent (F-3).
    /// </summary>
    public const string ApplicationIdKey = "NAFATH_APP_ID";

    /// <summary>Null unless the authority, both secrets and the callback URL are set and well formed.</summary>
    public static OpenIdConnectClient? Read(IConfiguration configuration, NafathOptions options)
    {
        Uri? authority = OpenIdConnectClient.AbsoluteUri(options.Authority);
        Uri? callback = OpenIdConnectClient.AbsoluteUri(configuration[CallbackUrlKey]);
        string? clientId = configuration[ApplicationSecrets.NafathClientId];
        string? clientSecret = configuration[ApplicationSecrets.NafathClientSecret];

        return authority is null || callback is null || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrEmpty(clientSecret)
            ? null
            : new OpenIdConnectClient(authority, clientId, clientSecret, callback);
    }
}
