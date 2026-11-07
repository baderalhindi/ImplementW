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

/// <summary>A signed-in account's new password, proved by its current one.</summary>
public sealed record OwnPasswordChangeRequest(string? CurrentPassword, string? NewPassword)
{
    private const int MaxPasswordLength = 200;

    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.Require(CurrentPassword, "currentPassword", 1024, errors);
        RequestValidation.RequireText(NewPassword, "newPassword", MaxPasswordLength, errors);
        return errors;
    }
}

/// <summary>Asks for a reset link for the account with this username.</summary>
public sealed record PasswordResetRequestCreateRequest(string? Username)
{
    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireText(Username, "username", 256, errors);
        return errors;
    }
}

/// <summary>A new password, with the token from a reset link.</summary>
public sealed record PasswordResetCompleteRequest(string? ResetToken, string? NewPassword)
{
    private const int MaxPasswordLength = 200;

    internal List<FieldError> Validate()
    {
        List<FieldError> errors = [];
        RequestValidation.RequireText(ResetToken, "resetToken", 4000, errors);
        RequestValidation.RequireText(NewPassword, "newPassword", MaxPasswordLength, errors);
        return errors;
    }
}

/// <summary>The answer to a sign-in with a temporary password: no session yet.</summary>
public sealed record PasswordChangePendingDetail(string PasswordChangeToken, DateTimeOffset ExpiresAt);
