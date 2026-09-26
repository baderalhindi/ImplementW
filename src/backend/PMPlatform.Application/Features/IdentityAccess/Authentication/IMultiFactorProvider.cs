using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// The MFA verification provider (<c>MFA_PROVIDER_ENDPOINT</c>, <c>MFA_PROVIDER_API_KEY</c>). It holds the factors; the
/// platform records only that a user is enrolled (<c>User.MfaEnrolledAt</c>). The person is identified to it by their
/// platform user id, never by a name or an email. Implemented in Infrastructure/Identity.
/// </summary>
public interface IMultiFactorProvider
{
    public bool IsConfigured { get; }

    public Task<MultiFactorChallengeResult> StartAsync(Guid userId, MultiFactorPurpose purpose, CancellationToken cancellationToken);

    /// <summary>Checks <paramref name="code"/> against the challenge; the provider binds a challenge to the user it was started for.</summary>
    public Task<MultiFactorVerification> CompleteAsync(Guid userId, MultiFactorPurpose purpose, string challengeId, string code, CancellationToken cancellationToken);
}
