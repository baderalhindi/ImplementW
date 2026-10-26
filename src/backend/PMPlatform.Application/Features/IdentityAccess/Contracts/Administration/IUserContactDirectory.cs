namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>What another module may learn about how to reach a user (E-U1: every module may query IdentityAccess).</summary>
public interface IUserContactDirectory
{
    /// <summary>
    /// The user's mobile number, only if its holder has verified it and the user is active; otherwise null. The SMS
    /// channel (TASK-103) sends to nothing else: an unverified number would send AHDA project information to a stranger (ADR-004).
    /// </summary>
    public Task<string?> FindVerifiedMobileNumberAsync(Guid userId, CancellationToken cancellationToken);
}
