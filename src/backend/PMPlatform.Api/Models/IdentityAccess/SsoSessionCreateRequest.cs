using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// Completes an SSO sign-in: the <c>code</c> and <c>state</c> the identity provider redirected to
/// <c>SSO_OIDC_CALLBACK_URL</c> with, and the <c>transaction</c> from <see cref="SsoAuthorizationDetail"/>.
/// </summary>
public sealed record SsoSessionCreateRequest(string? Code, string? State, string? Transaction)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(Code, "code", 4096, errors);
        RequestValidation.Require(State, "state", 512, errors);
        RequestValidation.Require(Transaction, "transaction", 4096, errors);
        return errors;
    }
}
