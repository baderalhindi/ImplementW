using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// Configuration section <c>Identity:Nafath</c> (TASK-068): whether Nafath verifies external users' identity at
/// onboarding. ADR-007 confirms the use: external entity users only, identity verification only, never internal sign-in.
/// OQ-007 keeps the data-minimisation boundary open, and its impact note says the module "can be built behind a feature
/// flag but should not go live until confirmed", so it is off unless an environment turns it on.
/// </summary>
public sealed class IdentityVerificationPolicy
{
    public const string Section = "Identity:Nafath";

    /// <summary>When true, an external user whose identity Nafath has not verified gets no session until it does.</summary>
    public bool Enabled { get; set; }

    /// <summary>What the API tells a client to wait before retrying a verification Nafath could not complete (<c>Retry-After</c>).</summary>
    public TimeSpan RetryAfter { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The one use case: an external user not yet verified, with the feature on. An internal user is never verified, and
    /// a verified user is not verified again: Nafath verifies once, at onboarding, and sign-in follows (ADR-013 note).
    /// </summary>
    public bool Requires(UserType userType, bool identityVerified) => Enabled && userType == UserType.External && !identityVerified;
}
