namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADR-004's verification step: a code goes to the user's mobile number and only its holder can confirm it. Until then
/// the number is unverified and no SMS goes to it (<see cref="IUserContactDirectory"/>). A user verifies their own number.
/// </summary>
public interface IMobileNumberVerificationService
{
    /// <summary>The person's own number and whether it is verified (13 Nov 2026 review: the account page's sign-in methods).</summary>
    public Task<AdministrationResult<MobileNumberStatus>> GetAsync(Guid userId, CancellationToken cancellationToken);

    public Task<AdministrationResult<MobileVerificationChallenge>> StartAsync(Guid userId, CancellationToken cancellationToken);

    public Task<AdministrationResult<MobileVerificationResult>> CompleteAsync(Guid userId, string challengeId, string code, CancellationToken cancellationToken);
}
