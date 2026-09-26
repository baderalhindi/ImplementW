using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Completes a sign-in with the second factor: the MFA token, the challenge and the code the person entered.</summary>
public sealed record MfaSessionCreateRequest(string? MfaToken, string? ChallengeId, string? Code)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(MfaToken, "mfaToken", 8192, errors);
        RequestValidation.RequireChallenge(ChallengeId, Code, errors);
        return errors;
    }
}
