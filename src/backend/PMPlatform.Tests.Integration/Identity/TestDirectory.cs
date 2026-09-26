namespace PMPlatform.Tests.Integration.Identity;

/// <summary>
/// The test directory, AHDA-ldap (infra/docker/ldap): <c>docker compose -f infra/docker/docker-compose.yml up -d --wait ldap</c>.
/// Its address is <c>AD_LDAP_URL</c> when set, else the compose stack's published port.
/// </summary>
internal static class TestDirectory
{
    public const string BindDn = "cn=svc-pmplatform,ou=Services,dc=pmplatform,dc=local";

    /// <summary>Local defaults from infra/docker/ldap/directory.ldif, never real credentials.</summary>
    public const string BindPassword = "local-service-password";

    public const string PersonPassword = "local-directory-password";

    public static string Url => Environment.GetEnvironmentVariable("AD_LDAP_URL") is { Length: > 0 } url
        ? url
        : "ldap://localhost:3389/dc=pmplatform,dc=local";

    /// <summary>The entryUUID of local.r0<paramref name="n"/>, which is that person's directory subject.</summary>
    public static string Subject(int n) => $"00000000-0012-4000-8000-{n:D12}";

    /// <summary>In the directory, with no platform user.</summary>
    public static readonly string UnregisteredSubject = Subject(99);

    /// <summary>The attribute names of an OpenLDAP inetOrgPerson, as the compose stack configures them.</summary>
    public static IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["AD_LDAP_URL"] = Url,
        ["AD_BIND_DN"] = BindDn,
        ["AD_BIND_PASSWORD"] = BindPassword,
        ["Identity:Directory:UsernameAttribute"] = "uid",
        ["Identity:Directory:SubjectAttribute"] = "entryUUID",
        ["Identity:Directory:DepartmentAttribute"] = "departmentNumber",
        ["Identity:Directory:AllowUnencryptedConnection"] = "true",
        ["Identity:Directory:Timeout"] = "00:00:05",
    };
}
