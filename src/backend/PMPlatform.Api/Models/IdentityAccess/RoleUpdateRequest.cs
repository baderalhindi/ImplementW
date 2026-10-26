using PMPlatform.Api.Errors;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>ADM-008: a canonical role's editable representation, its bilingual name.</summary>
public sealed record RoleUpdateRequest(BilingualLabelRequest? Name)
{
    internal List<FieldError> Validate(out BilingualLabel? name)
    {
        List<FieldError> errors = [];
        RequestValidation.Label(Name, "name", errors);
        name = errors.Count > 0 ? null : Name!.ToLabel();
        return errors;
    }
}
