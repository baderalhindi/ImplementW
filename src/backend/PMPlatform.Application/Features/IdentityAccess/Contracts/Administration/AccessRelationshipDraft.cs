namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-010 / MOD-081: assign a profile version, never a permission (ADR-018; no per-user grant, BR-IAM-023). For an
/// external user (ADR-013) the role must be external-eligible, the entity anchor is their own entity, a named AHDA sponsor
/// is required, and an R04 grant is per project, on a project their entity delivers. <see cref="StartsAt"/> defaults to now and is never in the past: an assignment does not backdate access.
/// </summary>
public sealed record AccessRelationshipDraft(
    Guid UserId,
    Guid PermissionProfileVersionId,
    Guid? DepartmentId,
    Guid? ExternalEntityId,
    Guid? ProjectId,
    Guid? SponsorUserId,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);
