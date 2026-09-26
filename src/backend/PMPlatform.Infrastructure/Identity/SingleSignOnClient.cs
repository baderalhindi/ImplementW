using Microsoft.Extensions.Configuration;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>The OIDC client registration, read at each use. Null unless all four values are set and well formed.</summary>
internal sealed record SingleSignOnClient(Uri Authority, string ClientId, string ClientSecret, Uri CallbackUrl)
{
    public const string AuthorityKey = "SSO_OIDC_AUTHORITY";
    public const string ClientIdKey = "SSO_OIDC_CLIENT_ID";
    public const string CallbackUrlKey = "SSO_OIDC_CALLBACK_URL";

    public static SingleSignOnClient? Read(IConfiguration configuration)
    {
        Uri? authority = AbsoluteUri(configuration[AuthorityKey]);
        Uri? callback = AbsoluteUri(configuration[CallbackUrlKey]);
        string? clientId = configuration[ClientIdKey];
        string? clientSecret = configuration[ApplicationSecrets.SsoClientSecret];

        return authority is null || callback is null || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrEmpty(clientSecret)
            ? null
            : new SingleSignOnClient(authority, clientId, clientSecret, callback);
    }

    public static Uri? AbsoluteUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http" ? uri : null;

    /// <summary><c>{authority}/.well-known/openid-configuration</c> (OpenID Connect Discovery §4).</summary>
    public Uri MetadataAddress => new($"{IssuerOf(Authority)}/.well-known/openid-configuration");

    /// <summary>The authority without a trailing slash, the form an issuer is compared in.</summary>
    public static string IssuerOf(Uri authority) => authority.AbsoluteUri.TrimEnd('/');

    /// <summary>The client secret never appears in a string form of this record.</summary>
    public override string ToString() => $"{nameof(SingleSignOnClient)} {{ Authority = {Authority}, ClientId = {ClientId}, CallbackUrl = {CallbackUrl} }}";
}
