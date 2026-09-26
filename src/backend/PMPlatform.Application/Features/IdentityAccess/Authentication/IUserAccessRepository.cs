namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>The platform side of a sign-in: the user a directory subject belongs to and their active assignments.</summary>
public interface IUserAccessRepository
{
    public Task<UserAccess?> FindByDirectorySubjectAsync(string directorySubjectId, CancellationToken cancellationToken);

    public Task<UserAccess?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Copies the directory-authoritative attributes onto the user (ADR-007). A department or manager the platform
    /// cannot resolve is left as it is and reported, never guessed.
    /// </summary>
    public Task ApplyDirectoryAttributesAsync(Guid userId, DirectoryEntry entry, CancellationToken cancellationToken);
}
