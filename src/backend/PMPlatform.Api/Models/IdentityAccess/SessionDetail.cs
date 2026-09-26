using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>An issued session. The access token goes in <c>Authorization: Bearer</c> (R-46); the refresh token only to the refresh command.</summary>
public sealed record SessionDetail(
    string TokenType,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    DateTimeOffset SessionExpiresAt,
    SessionUserDetail User)
{
    internal static SessionDetail From(PlatformSession session) => new(
        "Bearer",
        session.AccessToken,
        session.AccessTokenExpiresAt,
        session.RefreshToken,
        session.RefreshTokenExpiresAt,
        session.SessionExpiresAt,
        new SessionUserDetail(
            session.User.Id,
            session.User.UserType,
            session.User.Username,
            session.User.DisplayName,
            LanguageCode.Of(session.User.PreferredLanguage),
            session.User.AuthenticationMethod,
            [.. session.User.RoleAssignments.Select(a => new RoleAssignmentSummary(a.RoleCode, a.PermissionProfileVersionId, a.DepartmentId, a.ExternalEntityId, a.ProjectId))]));
}
