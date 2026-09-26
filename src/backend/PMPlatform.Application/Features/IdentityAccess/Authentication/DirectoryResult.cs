using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>A directory entry, or why there is none. Not found and wrong password are both <see cref="AuthenticationFailure.Rejected"/>.</summary>
public sealed record DirectoryResult(DirectoryEntry? Entry, AuthenticationFailure? Failure)
{
    public static DirectoryResult Found(DirectoryEntry entry) => new(entry, null);

    public static DirectoryResult Failed(AuthenticationFailure failure) => new(null, failure);
}
