using Microsoft.Extensions.Configuration;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>The SSO client registration at AHDA's identity provider (<c>SSO_OIDC_*</c>), read at each use.</summary>
internal static class SingleSignOnClient
{
    public const string AuthorityKey = "SSO_OIDC_AUTHORITY";
    public const string ClientIdKey = "SSO_OIDC_CLIENT_ID";
    public const string CallbackUrlKey = "SSO_OIDC_CALLBACK_URL";

    /// <summary>Null unless all four values are set and well formed.</summary>
    public static OpenIdConnectClient? Read(IConfiguration configuration)
    {
        Uri? authority = OpenIdConnectClient.AbsoluteUri(configuration[AuthorityKey]);
        Uri? callback = OpenIdConnectClient.AbsoluteUri(configuration[CallbackUrlKey]);
        string? clientId = configuration[ClientIdKey];
        string? clientSecret = configuration[ApplicationSecrets.SsoClientSecret];

        return authority is null || callback is null || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrEmpty(clientSecret)
            ? null
            : new OpenIdConnectClient(authority, clientId, clientSecret, callback);
    }
}
