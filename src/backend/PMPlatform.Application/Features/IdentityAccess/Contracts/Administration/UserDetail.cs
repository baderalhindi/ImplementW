using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-003 User Detail. <see cref="MobileVerifiedAt"/> is null until the holder of the number has confirmed it (ADR-004);
/// <see cref="PreferredLanguage"/> is <c>ar</c> or <c>en</c>. A disabled user keeps every attribute, so a record they owned
/// or acted on still names them (Appendix A.1). The Nafath reference itself is never returned (OQ-007).
/// </summary>
public sealed record UserDetail(
    Guid Id,
    UserType UserType,
    string? DirectorySubjectId,
    string Username,
    string DisplayName,
    string Email,
    string? MobileNumber,
    DateTimeOffset? MobileVerifiedAt,
    string? JobTitle,
    Guid? DepartmentId,
    Guid? ManagerUserId,
    Guid? ExternalEntityId,
    string PreferredLanguage,
    UserStatus Status,
    DateTimeOffset? DisabledAt,
    bool MultiFactorEnrolled,
    DateTimeOffset? NafathVerifiedAt,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
