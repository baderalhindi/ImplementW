using PMPlatform.Api.Errors;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// The first step of sign-in with biometrics: the person's National ID or Iqama number, ten digits. The passkey challenge
/// and the SMS code challenge both take it.
/// </summary>
public sealed record BiometricSignInRequest(string? NationalId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(NationalId, "nationalId", NationalIdNumber.Length, errors);
        if (errors.Count == 0 && !NationalIdNumber.IsValid(NationalId))
        {
            errors.Add(new FieldError("nationalId", FieldError.Malformed));
        }

        return errors;
    }
}

/// <summary>
/// Starts a passkey sign-in: with <see cref="NationalId"/> (sign-in with biometrics) only its owner's passkey will verify;
/// without it (the Face ID / Touch ID button) the passkey the device offers signs its owner in.
/// </summary>
public sealed record PasskeyChallengeRequest(string? NationalId)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        if (NationalId is not null && !NationalIdNumber.IsValid(NationalId))
        {
            errors.Add(new FieldError("nationalId", FieldError.Malformed));
        }

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
