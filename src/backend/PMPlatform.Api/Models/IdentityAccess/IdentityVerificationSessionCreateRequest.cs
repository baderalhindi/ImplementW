using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// Completes the Nafath identity verification: the sign-in's <c>identityVerificationToken</c>, the <c>code</c> and
/// <c>state</c> Nafath redirected to <c>NAFATH_CALLBACK_URL</c> with, and the <c>transaction</c> from
/// <see cref="IdentityVerificationAuthorizationDetail"/>.
/// </summary>
public sealed record IdentityVerificationSessionCreateRequest(string? IdentityVerificationToken, string? Code, string? State, string? Transaction)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(IdentityVerificationToken, "identityVerificationToken", 8192, errors);
        RequestValidation.Require(Code, "code", 4096, errors);
        RequestValidation.Require(State, "state", 512, errors);
        RequestValidation.Require(Transaction, "transaction", 4096, errors);
        return errors;
    }
}
