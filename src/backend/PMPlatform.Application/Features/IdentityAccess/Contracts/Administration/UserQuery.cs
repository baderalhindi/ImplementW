using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-002 filters (R-31): an empty set filters nothing. <see cref="Text"/> matches username, display name or email,
/// ignoring case.
/// </summary>
public sealed record UserQuery(
    IReadOnlyCollection<UserStatus> Statuses,
    IReadOnlyCollection<UserType> UserTypes,
    Guid? DepartmentId,
    Guid? ExternalEntityId,
    string? Text,
    UserSort Sort,
    PageRequest Page);
