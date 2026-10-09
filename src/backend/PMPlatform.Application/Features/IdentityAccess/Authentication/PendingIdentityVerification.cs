using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// A sign-in that passed every factor and waits on Nafath's identity verification, as a valid verification token carries
/// it. <see cref="Authentication"/> is how and when the person authenticated: the session issued after the verification
/// carries it unchanged.
/// </summary>
public sealed record PendingIdentityVerification(Guid UserId, SessionAuthentication Authentication);
