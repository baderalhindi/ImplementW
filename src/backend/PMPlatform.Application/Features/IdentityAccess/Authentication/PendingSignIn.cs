using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>A sign-in whose first factor passed and whose second is outstanding, as a valid MFA token carries it.</summary>
public sealed record PendingSignIn(Guid UserId, AuthenticationMethod Method);
