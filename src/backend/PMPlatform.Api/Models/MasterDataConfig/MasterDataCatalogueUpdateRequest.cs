using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>ADM-020–029: a catalogue's editable representation, its bilingual name.</summary>
public sealed record MasterDataCatalogueUpdateRequest(BilingualLabelRequest? Name)
{
    internal List<FieldError> Validate(out BilingualLabel? name)
    {
        List<FieldError> errors = [];
        RequestValidation.Label(Name, "name", errors);
        name = errors.Count > 0 ? null : Name!.ToLabel();
        return errors;
    }
}
