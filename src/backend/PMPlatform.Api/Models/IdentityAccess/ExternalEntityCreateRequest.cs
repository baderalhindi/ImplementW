using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>ADM-013: an external entity, ACTIVE on creation (ADR-013).</summary>
public sealed record ExternalEntityCreateRequest(string? Code, BilingualLabelRequest? Name, Guid? EntityTypeItemId, Guid? SponsorUserId)
{
    internal List<FieldError> Validate(out ExternalEntityDraft? draft)
    {
        List<FieldError> errors = [];
        RequestValidation.Code(Code, "code", errors);
        RequestValidation.Label(Name, "name", errors);
        RequestValidation.RequireId(EntityTypeItemId, "entityTypeItemId", errors);
        draft = errors.Count > 0 ? null : new ExternalEntityDraft(Code!, Name!.ToLabel(), EntityTypeItemId!.Value, SponsorUserId);
        return errors;
    }
}
