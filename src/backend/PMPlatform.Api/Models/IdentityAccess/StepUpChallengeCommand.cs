using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Starts a step-up of the session the refresh token continues (ADR-010).</summary>
public sealed record StepUpChallengeCommand(string? RefreshToken)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(RefreshToken, "refreshToken", 8192, errors);
        return errors;
    }
}
