using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Shape validation of request strings (T-3: shape here, meaning in the module).</summary>
internal static class RequestValidation
{
    public static void Require(string? value, string field, int maxLength, List<FieldError> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors.Add(new FieldError(field, FieldError.Required));
        }
        else if (value.Length > maxLength)
        {
            errors.Add(new FieldError(field, FieldError.MaxLength));
        }
    }
}
