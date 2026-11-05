using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>A hash a local account has used, so none of the last five comes back (the 5 Nov 2026 review, R4-6).</summary>
public sealed class UserPasswordHistory : AuditedEntity
{
    public Guid UserId { get; set; }

    public required string PasswordHash { get; set; }
}
