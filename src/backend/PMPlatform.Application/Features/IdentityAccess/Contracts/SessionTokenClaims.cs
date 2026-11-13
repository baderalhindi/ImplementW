using System.Globalization;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>The issuer, audiences, claim names and values of the platform's session tokens: the module's token contract.</summary>
public static class SessionTokenClaims
{
    public const string Issuer = "pmplatform";

    /// <summary>The API accepts only tokens for this audience, so a refresh token can never be used as an access token.</summary>
    public const string AccessAudience = "pmplatform-api";

    public const string RefreshAudience = "pmplatform-session-refresh";

    /// <summary>The MFA token between the first and the second factor (TASK-029). It grants nothing but the second factor.</summary>
    public const string MultiFactorAudience = "pmplatform-session-mfa";

    /// <summary>The identity verification token between the last factor and Nafath (TASK-068). It grants nothing but the verification.</summary>
    public const string IdentityVerificationAudience = "pmplatform-session-identity-verification";

    public const string Subject = "sub";
    public const string SessionId = "sid";
    public const string UserType = "user_type";
    public const string Role = "role";

    /// <summary>RFC 8176 authentication method references: the first factor, and <see cref="MultiFactorMethod"/> once a second is passed.</summary>
    public const string AuthenticationMethod = "amr";

    /// <summary>OIDC Core §2: when the person last authenticated interactively, as a NumericDate. Step-up is measured from it.</summary>
    public const string AuthenticatedAt = "auth_time";

    public const string ExpiresAt = "exp";
    public const string SessionExpiresAt = "session_exp";

    /// <summary>RFC 8176 "pwd": a password, here the person's directory password.</summary>
    public const string DirectoryMethod = "pwd";

    /// <summary>Single sign-on at AHDA's identity provider.</summary>
    public const string SingleSignOnMethod = "sso";

    /// <summary>RFC 8176 "pwd" is the directory's; a local account's password says so apart.</summary>
    public const string LocalPasswordMethod = "local-pwd";

    /// <summary>A passkey: a device-held key unlocked by the person's biometric or device unlock (13 Nov 2026 review).</summary>
    public const string PasskeyMethod = "passkey";

    /// <summary>RFC 8176 "sms": a code sent by SMS.</summary>
    public const string SmsCodeMethod = "sms";

    public const string PasswordChangeAudience = "pmplatform-session-password-change";

    public const string PasswordResetAudience = "pmplatform-password-reset";

    /// <summary>On a password reset token: which password it may replace, so it stops working once that password changes.</summary>
    public const string PasswordFingerprint = "pwf";

    /// <summary>RFC 8176 "mfa": a second factor has been passed.</summary>
    public const string MultiFactorMethod = "mfa";

    public static string MethodValue(AuthenticationMethod method) => method switch
    {
        Contracts.AuthenticationMethod.Directory => DirectoryMethod,
        Contracts.AuthenticationMethod.SingleSignOn => SingleSignOnMethod,
        Contracts.AuthenticationMethod.LocalPassword => LocalPasswordMethod,
        Contracts.AuthenticationMethod.Passkey => PasskeyMethod,
        Contracts.AuthenticationMethod.SmsCode => SmsCodeMethod,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };

    public static AuthenticationMethod? ParseMethod(string? value) => value switch
    {
        DirectoryMethod => Contracts.AuthenticationMethod.Directory,
        SingleSignOnMethod => Contracts.AuthenticationMethod.SingleSignOn,
        LocalPasswordMethod => Contracts.AuthenticationMethod.LocalPassword,
        PasskeyMethod => Contracts.AuthenticationMethod.Passkey,
        SmsCodeMethod => Contracts.AuthenticationMethod.SmsCode,
        _ => null,
    };

    /// <summary>The <c>amr</c> values a token carries for <paramref name="authentication"/>: the first factor, then <c>mfa</c> if passed.</summary>
    public static string[] MethodValues(SessionAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        return authentication.MultiFactor
            ? [MethodValue(authentication.Method), MultiFactorMethod]
            : [MethodValue(authentication.Method)];
    }

    /// <summary>
    /// Reads the authentication context back from a token's <c>amr</c> values and <c>auth_time</c>. Null unless there is
    /// exactly one first-factor method and a valid <c>auth_time</c>: a token that cannot say how its user signed in
    /// proves nothing.
    /// </summary>
    public static SessionAuthentication? ReadAuthentication(IEnumerable<string> methodValues, string? authenticatedAt)
    {
        ArgumentNullException.ThrowIfNull(methodValues);
        List<string> values = [.. methodValues];
        List<AuthenticationMethod> firstFactors = [.. values.Select(ParseMethod).OfType<AuthenticationMethod>()];
        return firstFactors.Count == 1
               && long.TryParse(authenticatedAt, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds)
            ? new SessionAuthentication(firstFactors[0], values.Contains(MultiFactorMethod, StringComparer.Ordinal), DateTimeOffset.FromUnixTimeSeconds(seconds))
            : null;
    }
}
