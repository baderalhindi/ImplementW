namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>A session, a second factor or an identity verification still to pass, or the reason there is none of these.</summary>
public sealed record AuthenticationResult
{
    private AuthenticationResult(
        PlatformSession? session, MultiFactorPending? multiFactorPending, IdentityVerificationPending? identityVerificationPending, AuthenticationFailure? failure)
    {
        Session = session;
        MultiFactorPending = multiFactorPending;
        IdentityVerificationPending = identityVerificationPending;
        Failure = failure;
    }

    public PlatformSession? Session { get; }

    public MultiFactorPending? MultiFactorPending { get; }

    public IdentityVerificationPending? IdentityVerificationPending { get; }

    public AuthenticationFailure? Failure { get; }

    public static AuthenticationResult Succeeded(PlatformSession session) => new(session, null, null, null);

    public static AuthenticationResult MultiFactorRequired(MultiFactorPending pending) => new(null, pending, null, null);

    public static AuthenticationResult IdentityVerificationRequired(IdentityVerificationPending pending) => new(null, null, pending, null);

    public static AuthenticationResult Failed(AuthenticationFailure failure) => new(null, null, null, failure);
}
