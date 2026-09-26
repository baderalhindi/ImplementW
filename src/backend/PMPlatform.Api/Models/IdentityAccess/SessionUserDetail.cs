using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

public sealed record SessionUserDetail(
    Guid Id,
    UserType UserType,
    string Username,
    string DisplayName,
    string PreferredLanguage,
    AuthenticationMethod AuthenticationMethod,
    IReadOnlyList<RoleAssignmentSummary> RoleAssignments);
