using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>A local account's new password, with the token its sign-in answered.</summary>
public sealed record PasswordChangeRequest(string? PasswordChangeToken, string? NewPassword)
{
    private const int MaxPasswordLength = 200;

    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireText(PasswordChangeToken, "passwordChangeToken", 4000, errors);
        RequestValidation.RequireText(NewPassword, "newPassword", MaxPasswordLength, errors);
        return errors;
    }
}

/// <summary>The answer to a sign-in with a temporary password: no session yet.</summary>
public sealed record PasswordChangePendingDetail(string PasswordChangeToken, DateTimeOffset ExpiresAt);
