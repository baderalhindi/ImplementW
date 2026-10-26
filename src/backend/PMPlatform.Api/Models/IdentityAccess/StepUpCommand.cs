using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// Completes a step-up: the session's refresh token, the challenge and the code. The answer is the session's next token
/// pair, whose authentication time is now.
/// </summary>
public sealed record StepUpCommand(string? RefreshToken, string? ChallengeId, string? Code)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(RefreshToken, "refreshToken", 8192, errors);
        RequestValidation.RequireChallenge(ChallengeId, Code, errors);
        return errors;
    }
}
