using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Asks for a sign-in code by SMS to a mobile number in E.164 (ADR-004), such as <c>+966501234567</c>.</summary>
public sealed record SmsCodeChallengeCreateRequest(string? MobileNumber)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(MobileNumber, "mobileNumber", 20, errors);
        RequestValidation.MobileNumber(MobileNumber, "mobileNumber", errors);
        return errors;
    }
}

/// <summary>Signs in with the code the SMS carried, for the challenge it was sent under.</summary>
public sealed record SmsSessionCreateRequest(Guid? ChallengeId, string? Code)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(ChallengeId, "challengeId", errors);
        RequestValidation.Require(Code, "code", 64, errors);
        return errors;
    }
}
