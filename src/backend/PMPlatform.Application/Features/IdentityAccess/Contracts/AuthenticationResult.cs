namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>A session, a second factor or an identity verification still to pass, or the reason there is none of these.</summary>
public sealed record AuthenticationResult
{
    private AuthenticationResult(
        PlatformSession? session, MultiFactorPending? multiFactorPending, IdentityVerificationPending? identityVerificationPending, AuthenticationFailure? failure,
        PasswordChangePending? passwordChangePending = null, string? passwordIssue = null)
    {
        PasswordChangePending = passwordChangePending;
        PasswordIssue = passwordIssue;
        Session = session;
        MultiFactorPending = multiFactorPending;
        IdentityVerificationPending = identityVerificationPending;
        Failure = failure;
    }

    public PlatformSession? Session { get; }

    public MultiFactorPending? MultiFactorPending { get; }

    public IdentityVerificationPending? IdentityVerificationPending { get; }

    public AuthenticationFailure? Failure { get; }

    public PasswordChangePending? PasswordChangePending { get; }

    /// <summary>The rule a new password broke: <see cref="Authentication.LocalPassword.TooWeak"/> or <see cref="Authentication.LocalPassword.Reused"/>.</summary>
    public string? PasswordIssue { get; }

    public static AuthenticationResult PasswordChangeRequired(PasswordChangePending pending) => new(null, null, null, null, pending);

    public static AuthenticationResult PasswordRejected(string issue) => new(null, null, null, null, null, issue);

    public static AuthenticationResult Succeeded(PlatformSession session) => new(session, null, null, null);

    public static AuthenticationResult MultiFactorRequired(MultiFactorPending pending) => new(null, pending, null, null);

    public static AuthenticationResult IdentityVerificationRequired(IdentityVerificationPending pending) => new(null, null, pending, null);

    public static AuthenticationResult Failed(AuthenticationFailure failure) => new(null, null, null, failure);
}
