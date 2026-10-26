using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>ADM-010 / MOD-081: a profile version to bind the user to, with its scope anchors and period (ADR-018, ADR-013).</summary>
public sealed record AccessRelationshipCreateRequest(
    Guid? UserId,
    Guid? PermissionProfileVersionId,
    Guid? DepartmentId,
    Guid? ExternalEntityId,
    Guid? ProjectId,
    Guid? SponsorUserId,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt)
{
    internal List<FieldError> Validate(out AccessRelationshipDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(UserId, "userId", errors);
        RequestValidation.RequireId(PermissionProfileVersionId, "permissionProfileVersionId", errors);

        draft = errors.Count > 0
            ? null
            : new AccessRelationshipDraft(UserId!.Value, PermissionProfileVersionId!.Value, DepartmentId, ExternalEntityId, ProjectId, SponsorUserId, StartsAt, EndsAt);
        return errors;
    }
}
