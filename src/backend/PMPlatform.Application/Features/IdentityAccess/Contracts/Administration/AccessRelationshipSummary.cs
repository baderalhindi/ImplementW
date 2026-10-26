using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A row of ADM-010 Role Assignment: a user bound to a profile version with its scope anchors (ADR-018).</summary>
public sealed record AccessRelationshipSummary(
    Guid Id,
    Guid UserId,
    string RoleCode,
    Guid PermissionProfileVersionId,
    Guid? DepartmentId,
    Guid? ExternalEntityId,
    Guid? ProjectId,
    Guid? SponsorUserId,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    AccessEndReason? EndReason,
    AccessRelationshipStatus Status);
