using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>What the access token asserts: who, which kind of user, how and when they authenticated, and the role codes they hold.</summary>
public sealed record SessionTokenSubject(Guid UserId, UserType UserType, SessionAuthentication Authentication, IReadOnlyList<string> RoleCodes);
