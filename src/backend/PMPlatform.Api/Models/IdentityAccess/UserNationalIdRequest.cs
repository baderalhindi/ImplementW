using PMPlatform.Api.Errors;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>A user's National ID (starts with 1) or Iqama number (starts with 2), ten digits with a valid check digit; null removes it.</summary>
public sealed record UserNationalIdRequest(string? NationalId = null)
{
    internal List<FieldError> Validate(out string? nationalId)
    {
        List<FieldError> errors = [];
        nationalId = NationalId?.Trim();
        if (nationalId is not null && !NationalIdNumber.IsValid(nationalId))
        {
            errors.Add(new FieldError("nationalId", FieldError.Malformed));
        }

        return errors;
    }
}
