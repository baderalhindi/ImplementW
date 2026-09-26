using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>What the access token asserts: who, which kind of user, how they signed in, and the role codes they hold.</summary>
public sealed record SessionTokenSubject(Guid UserId, UserType UserType, AuthenticationMethod Method, IReadOnlyList<string> RoleCodes);
