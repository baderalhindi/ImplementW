using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A row of ADM-002 User List.</summary>
public sealed record UserSummary(
    Guid Id,
    UserType UserType,
    string Username,
    string DisplayName,
    string Email,
    UserStatus Status,
    Guid? DepartmentId,
    Guid? ExternalEntityId);
