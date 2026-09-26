using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>What the presented access token asserts.</summary>
public sealed record CurrentSessionDetail(
    Guid UserId,
    Guid SessionId,
    UserType UserType,
    AuthenticationMethod? AuthenticationMethod,
    IReadOnlyList<string> Roles,
    DateTimeOffset ExpiresAt);
