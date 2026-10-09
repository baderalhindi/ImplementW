using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Starts the Nafath identity verification with the token the sign-in returned.</summary>
public sealed record IdentityVerificationAuthorizationCreateRequest(string? IdentityVerificationToken)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(IdentityVerificationToken, "identityVerificationToken", 8192, errors);
        return errors;
    }
}
