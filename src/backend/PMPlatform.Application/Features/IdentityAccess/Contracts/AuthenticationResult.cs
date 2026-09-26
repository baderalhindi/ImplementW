namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>A session, or the reason there is none.</summary>
public sealed record AuthenticationResult
{
    private AuthenticationResult(PlatformSession? session, AuthenticationFailure? failure)
    {
        Session = session;
        Failure = failure;
    }

    public PlatformSession? Session { get; }

    public AuthenticationFailure? Failure { get; }

    public static AuthenticationResult Succeeded(PlatformSession session) => new(session, null);

    public static AuthenticationResult Failed(AuthenticationFailure failure) => new(null, failure);
}
