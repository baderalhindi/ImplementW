namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>What the directory answered to a person changing their own password.</summary>
public enum DirectoryPasswordChange
{
    Changed = 0,

    /// <summary>The bind with the current password failed. The directory's reason (wrong, locked, expired) stays in the directory.</summary>
    CurrentPasswordRejected = 1,

    /// <summary>The directory's password policy refused the new password.</summary>
    NewPasswordRejected = 2,

    /// <summary>No single entry has this subject, or the directory does not let this person change their password.</summary>
    NotPermitted = 3,

    Unavailable = 4,
}

/// <summary>A platform user whose password lives in the directory.</summary>
public sealed record DirectoryAccount(Guid UserId, string Username, string SubjectId, DateTimeOffset? PasswordChangedAt)
{
    /// <summary>Names this account's last platform change in a reset token, so the link stops working once it has been used.</summary>
    public string ResetFingerprint => Authentication.LocalPassword.Fingerprint($"directory:{SubjectId}:{PasswordChangedAt?.ToUnixTimeSeconds() ?? 0}");
}
