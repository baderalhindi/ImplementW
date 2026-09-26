using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Starts the second factor of a sign-in with the MFA token the first factor returned.</summary>
public sealed record MfaChallengeCreateRequest(string? MfaToken)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(MfaToken, "mfaToken", 8192, errors);
        return errors;
    }
}
