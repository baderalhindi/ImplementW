using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>The code the user received on their mobile number, for the challenge it was sent under (ADR-004).</summary>
public sealed record UserMobileVerificationCommand(string? ChallengeId, string? Code)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireChallenge(ChallengeId, Code, errors);
        return errors;
    }
}
