namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADR-004's verification step: a code goes to the user's mobile number and only its holder can confirm it. Until then
/// the number is unverified and no SMS goes to it (<see cref="IUserContactDirectory"/>). A user verifies their own number.
/// </summary>
public interface IMobileNumberVerificationService
{
    public Task<AdministrationResult<MobileVerificationChallenge>> StartAsync(Guid userId, CancellationToken cancellationToken);

    public Task<AdministrationResult<MobileVerificationResult>> CompleteAsync(Guid userId, string challengeId, string code, CancellationToken cancellationToken);
}
