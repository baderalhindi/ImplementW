namespace PMPlatform.Infrastructure.Identity.Nafath;

/// <summary>
/// Configuration section <c>Identity:Nafath</c>: the non-secret half of the Nafath client (TASK-068). The feature flag
/// beside it, <c>Enabled</c>, is the Application's <see cref="Application.Features.IdentityAccess.Contracts.IdentityVerificationPolicy"/>.
/// The client itself — <c>NAFATH_CLIENT_ID</c> and <c>NAFATH_CLIENT_SECRET</c> (secret store), <c>NAFATH_CALLBACK_URL</c>
/// (configuration store) — is read by <see cref="NafathClient"/>.
/// </summary>
internal sealed class NafathOptions
{
    public const string Section = "Identity:Nafath";

    /// <summary>
    /// Nafath's OpenID Connect issuer for this environment, sandbox or production. The Environment and Secrets sheet has
    /// no variable for it, so it is configuration (F-2); it is confirmed with AHDA IT when Nafath registers the client.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// <c>openid</c> alone: the verification needs only Nafath's assertion that it identified the person. No profile,
    /// national-id or other attribute scope is requested (OQ-007, nafath-data-minimisation.md).
    /// </summary>
    public IReadOnlyList<string> Scopes { get; set; } = ["openid"];

    /// <summary>Discovery and keys only over HTTPS. Cleared only by the tests' in-process Nafath.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>How long a started verification may take at Nafath before its transaction expires.</summary>
    public TimeSpan TransactionLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
