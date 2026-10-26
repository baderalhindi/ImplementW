using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// What the presented access token asserts. <c>authenticatedAt</c> tells the client how long before a step-up
/// operation will answer <c>STEP_UP_REQUIRED</c>.
/// </summary>
public sealed record CurrentSessionDetail(
    Guid UserId,
    Guid SessionId,
    UserType UserType,
    AuthenticationMethod? AuthenticationMethod,
    bool MultiFactorAuthenticated,
    DateTimeOffset? AuthenticatedAt,
    IReadOnlyList<string> Roles,
    DateTimeOffset ExpiresAt);
