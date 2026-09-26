using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

public sealed record SessionRefreshCommand(string? RefreshToken)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(RefreshToken, "refreshToken", 8192, errors);
        return errors;
    }
}
