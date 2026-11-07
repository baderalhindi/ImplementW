using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The outcome of a person changing their own password or finishing a reset. No session comes of it: every session of the account has just ended,
/// so the person signs in with the new password and passes their second factor as usual.
/// </summary>
public sealed record PasswordUpdateResult
{
    private PasswordUpdateResult(AuthenticationFailure? failure, string? issueField, string? issue, bool notLocalAccount)
    {
        Failure = failure;
        IssueField = issueField;
        Issue = issue;
        NotLocalAccount = notLocalAccount;
    }

    public static PasswordUpdateResult Updated { get; } = new(null, null, null, notLocalAccount: false);

    /// <summary>The account's password can't be changed from here: it belongs to the identity provider, or the directory refuses this person's change.</summary>
    public static PasswordUpdateResult NotLocal { get; } = new(null, null, null, notLocalAccount: true);

    public AuthenticationFailure? Failure { get; }

    public string? IssueField { get; }

    public string? Issue { get; }

    public bool NotLocalAccount { get; }

    public static PasswordUpdateResult Failed(AuthenticationFailure failure) => new(failure, null, null, notLocalAccount: false);

    public static PasswordUpdateResult Refused(string field, string issue) => new(null, field, issue, notLocalAccount: false);
}

/// <summary>What a password reset link grants: one account, while its password is the one the link was issued against.</summary>
public sealed record PasswordResetGrant(Guid UserId, string PasswordFingerprint);

/// <summary>A signed password reset token and when it stops working.</summary>
public sealed record PasswordResetLink(string Token, DateTimeOffset ExpiresAt)
{
    public override string ToString() => $"{nameof(PasswordResetLink)} {{ ExpiresAt = {ExpiresAt:O} }}";
}

/// <summary>One reset e-mail waiting to be sent. It holds a live credential, so it exists only in memory and never prints the token.</summary>
public sealed record PasswordResetMail(Guid UserId, string To, Language Language, PasswordResetLink Link)
{
    public override string ToString() => $"{nameof(PasswordResetMail)} {{ UserId = {UserId} }}";
}
