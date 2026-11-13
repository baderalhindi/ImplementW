using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>The platform side of a sign-in: the user a directory subject belongs to and their active assignments.</summary>
public interface IUserAccessRepository
{
    public Task<UserAccess?> FindByDirectorySubjectAsync(string directorySubjectId, CancellationToken cancellationToken);

    public Task<UserAccess?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user whose National ID or Iqama number has this fingerprint (client review, item 7), if any.</summary>
    public Task<Guid?> FindByNationalIdFingerprintAsync(string fingerprint, CancellationToken cancellationToken);

    /// <summary>The mobile number on the user's profile, verified or not.</summary>
    public Task<string?> FindMobileNumberAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Marks the number verified (ADR-004) if it is still the user's and not yet verified: a sign-in code sent to it was entered.</summary>
    public Task RecordMobileNumberVerifiedAsync(Guid userId, string mobileNumber, CancellationToken cancellationToken);

    /// <summary>The fingerprint of the National ID or Iqama number recorded for the user, if any (client review, item 7).</summary>
    public Task<string?> FindNationalIdFingerprintAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>A local account's credential by its username; null for a directory account or no account.</summary>
    public Task<LocalCredential?> FindLocalCredentialAsync(string username, CancellationToken cancellationToken);

    /// <summary>The user, if their password lives in the directory.</summary>
    public Task<DirectoryAccount?> FindDirectoryAccountAsync(Guid userId, CancellationToken cancellationToken);

    public Task<DirectoryAccount?> FindDirectoryAccountAsync(string username, CancellationToken cancellationToken);

    /// <summary>A directory password has changed: stamps the time, which ends the account's sessions at their next refresh.</summary>
    public Task RecordDirectoryPasswordChangedAsync(Guid userId, CancellationToken cancellationToken);

    public Task<LocalCredential?> FindLocalCredentialByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Counts a wrong password; the attempt that reaches <paramref name="maxAttempts"/> locks the account until <paramref name="lockedUntil"/> and starts the count again.</summary>
    public Task RecordFailedPasswordAsync(Guid userId, int maxAttempts, DateTimeOffset lockedUntil, CancellationToken cancellationToken);

    public Task ClearFailedPasswordsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The hashes of the account's last passwords, newest first.</summary>
    public Task<IReadOnlyList<string>> ListPasswordHistoryAsync(Guid userId, int count, CancellationToken cancellationToken);

    /// <summary>Sets a password the person chose: no longer temporary, kept in the history.</summary>
    public Task ChangePasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken);

    /// <summary>Claims this account's next reset e-mail, unless one went at or after <paramref name="notSince"/>. One statement, so two requests at once send one e-mail.</summary>
    public Task<bool> TryClaimPasswordResetAsync(Guid userId, DateTimeOffset now, DateTimeOffset notSince, CancellationToken cancellationToken);

    /// <summary>
    /// Copies the directory-authoritative attributes onto the user (ADR-007). A department or manager the platform
    /// cannot resolve is left as it is and reported, never guessed.
    /// </summary>
    public Task ApplyDirectoryAttributesAsync(Guid userId, DirectoryEntry entry, CancellationToken cancellationToken);

    /// <summary>Records that the user has enrolled a second factor (TASK-029). The change is the user's own.</summary>
    public Task RecordMultiFactorEnrolmentAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Records that Nafath verified the user's identity (TASK-068): the verification's reference and time, nothing else
    /// (OQ-007). The first verification stands; a later one changes nothing. The change is the user's own.
    /// </summary>
    public Task RecordIdentityVerificationAsync(Guid userId, string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Active internal users with a directory subject, with their job title, their department's directory reference and their manager's
    /// subject, ordered by id: what a directory reconciliation compares (TASK-075). Reads only.
    /// </summary>
    public Task<IReadOnlyList<DirectoryLinkedPerson>> ListDirectoryLinkedPeopleAsync(CancellationToken cancellationToken);
}
