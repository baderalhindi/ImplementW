namespace PMPlatform.Infrastructure.Secrets;

/// <summary>
/// The secrets PMPlatform.Api reads (TASK-019). Every entry is a Variable Name from the Environment and
/// Secrets sheet, and the application reads its value from the approved secret store at runtime — never
/// from a checked-in file (CTL-18).
/// </summary>
/// <remarks>
/// A key is added here by the task that first reads it, not in advance: the provider requires every key
/// on <see cref="Keys"/> at start-up, so listing a variable before any code consumes it would refuse to start an
/// environment over a value nothing needs yet. The full per-environment inventory the store holds is
/// <c>infra/environments/environments.json</c>, derived from the sheet by <c>infra/secrets/inventory.py</c>;
/// <c>docs/architecture/secret-management-check.py</c> fails the build if a key here is missing from it.
/// </remarks>
public static class ApplicationSecrets
{
    /// <summary>TASK-014, read by <see cref="Persistence.PostgreSqlHealthCheck"/> and by the data tier.</summary>
    public const string DatabaseConnectionString = "DB_CONNECTION_STRING";

    /// <summary>TASK-028: signs and validates the platform's session tokens.</summary>
    public const string JwtSigningKey = "JWT_SIGNING_KEY";

    /// <summary>TASK-028: AHDA's directory, as an LDAP URL whose path is the user search base.</summary>
    public const string DirectoryUrl = "AD_LDAP_URL";

    /// <summary>TASK-028: the directory service account.</summary>
    public const string DirectoryBindDn = "AD_BIND_DN";

    /// <summary>TASK-028: the directory service account's password.</summary>
    public const string DirectoryBindPassword = "AD_BIND_PASSWORD";

    /// <summary>TASK-028: the platform's client secret at AHDA's identity provider.</summary>
    public const string SsoClientSecret = "SSO_OIDC_CLIENT_SECRET";

    /// <summary>The configuration keys loaded from the secret store and required at start-up, in the order they are read.</summary>
    public static readonly IReadOnlyList<string> Keys = [DatabaseConnectionString, JwtSigningKey];

    /// <summary>
    /// Keys loaded from the secret store when they hold a value. Each belongs to a sign-in method that an environment
    /// may not offer yet: the sheet scopes the directory to SIT/UAT/PROD, and the SSO client exists only once AHDA IT
    /// registers it (ADR-007). Without them the method reports "not configured" on ADM-041 and the API still starts.
    /// </summary>
    public static readonly IReadOnlyList<string> OptionalKeys = [DirectoryUrl, DirectoryBindDn, DirectoryBindPassword, SsoClientSecret];
}
