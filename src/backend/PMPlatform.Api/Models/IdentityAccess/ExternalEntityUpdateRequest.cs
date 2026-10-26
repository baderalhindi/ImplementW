using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>ADM-013: the entity's editable representation in full; the code is fixed.</summary>
public sealed record ExternalEntityUpdateRequest(BilingualLabelRequest? Name, Guid? EntityTypeItemId, Guid? SponsorUserId)
{
    internal List<FieldError> Validate(out ExternalEntityChanges? changes)
    {
        List<FieldError> errors = [];
        RequestValidation.Label(Name, "name", errors);
        RequestValidation.RequireId(EntityTypeItemId, "entityTypeItemId", errors);
        changes = errors.Count > 0 ? null : new ExternalEntityChanges(Name!.ToLabel(), EntityTypeItemId!.Value, SponsorUserId);
        return errors;
    }
}
