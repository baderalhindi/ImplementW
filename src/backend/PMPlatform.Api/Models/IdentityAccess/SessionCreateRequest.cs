using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Sign-in with directory credentials.</summary>
public sealed record SessionCreateRequest(string? Username, string? Password)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(Username, "username", 256, errors);
        RequestValidation.Require(Password, "password", 1024, errors);
        return errors;
    }
}
