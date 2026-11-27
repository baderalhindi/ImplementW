using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// A signed-in session (SR-10, AUTH-T13). Its <see cref="AuditedEntity.Id"/> is the tokens' <c>sid</c>. Only the refresh
/// token named by <see cref="RefreshTokenId"/> continues it; each refresh replaces that id, so a refresh token works once,
/// and presenting one already replaced ends the session. A session that has ended is never continued.
/// </summary>
public sealed class UserSession : AuditedEntity
{
    public Guid UserId { get; set; }

    public UserSessionStatus Status { get; set; }

    /// <summary>The <c>jti</c> of the one refresh token that may continue the session next.</summary>
    public Guid RefreshTokenId { get; set; }

    /// <summary>The absolute expiry from sign-in, as the tokens' <c>session_exp</c> carries it; no refresh extends it.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public UserSessionEndReason? EndReason { get; set; }
}

public enum UserSessionStatus
{
    Active = 1,

    /// <summary>The person signed out.</summary>
    SignedOut = 2,

    /// <summary>The platform ended it: see <see cref="UserSessionEndReason"/>.</summary>
    Revoked = 3,
}

public enum UserSessionEndReason
{
    SignedOut = 1,

    /// <summary>A refresh token the session had already replaced was presented again: it was copied, so neither copy may continue.</summary>
    RefreshTokenReused = 2,

    /// <summary>An administrator disabled the user.</summary>
    UserDisabled = 3,

    /// <summary>An administrator deleted the user.</summary>
    UserDeleted = 4,
}
