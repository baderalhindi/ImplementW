using Microsoft.Extensions.Configuration;
using PMPlatform.Infrastructure.Secrets;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// <c>AD_LDAP_URL</c>, <c>AD_BIND_DN</c> and <c>AD_BIND_PASSWORD</c>, read at each use so a rotated service-account
/// password applies without a restart. <c>AD_LDAP_URL</c> is an RFC 4516 LDAP URL whose DN is the user search base,
/// e.g. <c>ldaps://dc01.ahda.example:636/OU=Staff,DC=ahda,DC=example</c>, so the sheet needs no separate variable for it.
/// </summary>
internal sealed record DirectoryConnection(string Host, int Port, bool UseTransportSecurity, string SearchBase, string BindDn, string BindPassword)
{
    private const int LdapPort = 389;
    private const int LdapsPort = 636;

    /// <summary>The connection, or null when any of the three variables is unset or the URL is not an LDAP URL with a base DN.</summary>
    public static DirectoryConnection? Read(IConfiguration configuration)
    {
        string? url = configuration[ApplicationSecrets.DirectoryUrl];
        string? bindDn = configuration[ApplicationSecrets.DirectoryBindDn];
        string? bindPassword = configuration[ApplicationSecrets.DirectoryBindPassword];

        if (string.IsNullOrWhiteSpace(bindDn) || string.IsNullOrEmpty(bindPassword) || !TryParseUrl(url, out Uri? address))
        {
            return null;
        }

        bool secure = address.Scheme == "ldaps";
        string searchBase = Uri.UnescapeDataString(address.AbsolutePath.TrimStart('/'));
        return string.IsNullOrWhiteSpace(searchBase)
            ? null
            : new DirectoryConnection(address.Host, address.IsDefaultPort || address.Port < 0 ? (secure ? LdapsPort : LdapPort) : address.Port, secure, searchBase, bindDn, bindPassword);
    }

    public static bool IsUrlSet(IConfiguration configuration) => TryParseUrl(configuration[ApplicationSecrets.DirectoryUrl], out _);

    public static bool IsUrlSecure(IConfiguration configuration) =>
        TryParseUrl(configuration[ApplicationSecrets.DirectoryUrl], out Uri? address) && address.Scheme == "ldaps";

    private static bool TryParseUrl(string? url, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Uri? address) =>
        Uri.TryCreate(url, UriKind.Absolute, out address) && address.Scheme is "ldap" or "ldaps" && address.Host.Length > 0;

    /// <summary>The bind password never appears in a string form of this record.</summary>
    public override string ToString() => $"{nameof(DirectoryConnection)} {{ Host = {Host}, Port = {Port}, UseTransportSecurity = {UseTransportSecurity}, SearchBase = {SearchBase} }}";
}
