using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// Nafath, the national identity verification service (<c>NAFATH_*</c>, TASK-068). It verifies an external user's
/// identity once, at onboarding; it never signs anyone in. Implemented in Infrastructure/Identity/Nafath.
/// </summary>
public interface IIdentityVerificationProvider
{
    public bool IsConfigured { get; }

    /// <summary>Starts a verification of <paramref name="userId"/>: Nafath's authorization URL and a transaction bound to that user.</summary>
    public Task<IdentityVerificationAuthorization> BeginAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Completes it with what Nafath redirected back with. <see cref="AuthenticationFailure.Rejected"/> when Nafath did not
    /// verify the person or the request is not this user's verification; <see cref="AuthenticationFailure.ProviderUnavailable"/>
    /// when Nafath cannot be reached or refuses the platform's client.
    /// </summary>
    public Task<IdentityVerificationResult> CompleteAsync(Guid userId, string code, string state, string transaction, CancellationToken cancellationToken);

    public IdentityVerificationIntegrationStatus Describe();

    /// <summary>Resolves Nafath's discovery document and checks its issuer against the configured authority.</summary>
    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);
}
