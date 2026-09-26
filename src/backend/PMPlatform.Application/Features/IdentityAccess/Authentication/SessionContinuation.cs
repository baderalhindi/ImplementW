using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>The session a valid refresh token belongs to. Its absolute expiry is carried forward unchanged.</summary>
public sealed record SessionContinuation(Guid UserId, Guid SessionId, AuthenticationMethod Method, DateTimeOffset SessionExpiresAt);
