using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>An optional bilingual text (ADR-012): absent, or both languages present and not blank.</summary>
internal static class OptionalLabel
{
    public static BilingualLabel? Validate(BilingualLabelRequest? value, string field, List<FieldError> errors)
    {
        if (value is null)
        {
            return null;
        }

        int before = errors.Count;
        RequestValidation.Label(value, field, errors);
        return errors.Count == before ? value.ToLabel() : null;
    }
}
