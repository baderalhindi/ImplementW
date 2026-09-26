namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>A session, a second factor still to pass, or the reason there is neither.</summary>
public sealed record AuthenticationResult
{
    private AuthenticationResult(PlatformSession? session, MultiFactorPending? multiFactorPending, AuthenticationFailure? failure)
    {
        Session = session;
        MultiFactorPending = multiFactorPending;
        Failure = failure;
    }

    public PlatformSession? Session { get; }

    public MultiFactorPending? MultiFactorPending { get; }

    public AuthenticationFailure? Failure { get; }

    public static AuthenticationResult Succeeded(PlatformSession session) => new(session, null, null);

    public static AuthenticationResult MultiFactorRequired(MultiFactorPending pending) => new(null, pending, null);

    public static AuthenticationResult Failed(AuthenticationFailure failure) => new(null, null, failure);
}
