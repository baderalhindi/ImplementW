namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>The issuer, audiences, claim names and values of the platform's session tokens: the module's token contract.</summary>
public static class SessionTokenClaims
{
    public const string Issuer = "pmplatform";

    /// <summary>The API accepts only tokens for this audience, so a refresh token can never be used as an access token.</summary>
    public const string AccessAudience = "pmplatform-api";

    public const string RefreshAudience = "pmplatform-session-refresh";

    public const string Subject = "sub";
    public const string SessionId = "sid";
    public const string UserType = "user_type";
    public const string Role = "role";
    public const string AuthenticationMethod = "amr";
    public const string ExpiresAt = "exp";
    public const string SessionExpiresAt = "session_exp";

    /// <summary>RFC 8176 "pwd": a password, here the person's directory password.</summary>
    public const string DirectoryMethod = "pwd";

    /// <summary>Single sign-on at AHDA's identity provider.</summary>
    public const string SingleSignOnMethod = "sso";

    public static string MethodValue(AuthenticationMethod method) => method switch
    {
        Contracts.AuthenticationMethod.Directory => DirectoryMethod,
        Contracts.AuthenticationMethod.SingleSignOn => SingleSignOnMethod,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };

    public static AuthenticationMethod? ParseMethod(string? value) => value switch
    {
        DirectoryMethod => Contracts.AuthenticationMethod.Directory,
        SingleSignOnMethod => Contracts.AuthenticationMethod.SingleSignOn,
        _ => null,
    };
}
