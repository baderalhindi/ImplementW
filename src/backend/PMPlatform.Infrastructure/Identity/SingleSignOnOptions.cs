namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// Configuration section <c>Identity:Sso</c>: the non-secret half of the OIDC client. The client itself —
/// <c>SSO_OIDC_AUTHORITY</c>, <c>SSO_OIDC_CLIENT_ID</c>, <c>SSO_OIDC_CALLBACK_URL</c> (configuration store) and
/// <c>SSO_OIDC_CLIENT_SECRET</c> (secret store) — is read by <see cref="SingleSignOnClient"/>.
/// </summary>
internal sealed class SingleSignOnOptions
{
    public const string Section = "Identity:Sso";

    /// <summary>
    /// The ID token claim holding the person's directory subject, matched against <c>User.DirectorySubjectId</c>. It must
    /// carry the same value as <see cref="DirectoryOptions.SubjectAttribute"/>; which claim does depends on AHDA's identity
    /// provider (confirmed at environment setup, ADR-007).
    /// </summary>
    public string SubjectClaim { get; set; } = "sub";

    /// <summary>No group or role scope is requested: platform roles are not taken from the identity provider (ADR-007).</summary>
    public IReadOnlyList<string> Scopes { get; set; } = ["openid"];

    /// <summary>Asked for on top of <see cref="Scopes"/>, whatever an environment configures: the e-mail check needs them (client review, item 7).</summary>
    public static IReadOnlyList<string> EmailScopes { get; } = ["profile", "email"];

    /// <summary>
    /// The ID token claims that carry the account e-mail, the first present wins (client review, item 7). Entra ID sends <c>email</c> with the
    /// email scope when the account has one, and <c>preferred_username</c>, normally the sign-in address, with the profile scope.
    /// </summary>
    public IReadOnlyList<string> EmailClaims { get; set; } = ["email", "preferred_username"];

    /// <summary>Discovery and keys only over HTTPS. Cleared only by the tests' in-process identity provider.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>How long a started sign-in may take at the identity provider before its transaction expires.</summary>
    public TimeSpan TransactionLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
