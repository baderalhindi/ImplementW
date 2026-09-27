using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>One assignment, with the profile and version it is bound to and who made and last changed it.</summary>
public sealed record AccessRelationshipDetail(
    Guid Id,
    Guid UserId,
    string RoleCode,
    Guid PermissionProfileId,
    string PermissionProfileCode,
    Guid PermissionProfileVersionId,
    int PermissionProfileVersionNo,
    Guid? DepartmentId,
    Guid? ExternalEntityId,
    Guid? ProjectId,
    Guid? SponsorUserId,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    AccessEndReason? EndReason,
    AccessRelationshipStatus Status,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
