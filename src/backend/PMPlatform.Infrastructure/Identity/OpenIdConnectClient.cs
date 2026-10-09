namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// One OpenID Connect client registration: the provider's authority and the platform's client id, client secret and
/// registered callback URL. AHDA's identity provider (<see cref="SingleSignOnClient"/>) and Nafath
/// (<see cref="Nafath.NafathClient"/>) each read theirs from configuration at every use.
/// </summary>
internal sealed record OpenIdConnectClient(Uri Authority, string ClientId, string ClientSecret, Uri CallbackUrl)
{
    /// <summary><c>{authority}/.well-known/openid-configuration</c> (OpenID Connect Discovery §4).</summary>
    public Uri MetadataAddress => new($"{IssuerOf(Authority)}/.well-known/openid-configuration");

    /// <summary>The authority without a trailing slash, the form an issuer is compared in.</summary>
    public static string IssuerOf(Uri authority) => authority.AbsoluteUri.TrimEnd('/');

    /// <summary>An absolute http or https URL, or null.</summary>
    public static Uri? AbsoluteUri(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme is "https" or "http" ? uri : null;

    /// <summary>The client secret never appears in a string form of this record.</summary>
    public override string ToString() => $"{nameof(OpenIdConnectClient)} {{ Authority = {Authority}, ClientId = {ClientId}, CallbackUrl = {CallbackUrl} }}";
}
