using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// A department. <see cref="DirectoryReference"/> is the directory object it stands for: sign-in resolves an internal
/// user's department through it (ADR-007).
/// </summary>
public sealed record DepartmentDetail(
    Guid Id,
    string Code,
    BilingualLabel Name,
    Guid? ParentDepartmentId,
    string? DirectoryReference,
    bool IsActive,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
