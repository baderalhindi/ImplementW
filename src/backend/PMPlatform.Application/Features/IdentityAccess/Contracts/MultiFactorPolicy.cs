namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// Configuration section <c>Identity:Mfa</c> (TASK-029, CTL-07): who must pass a second factor before a session is
/// issued. PTBC-027 leaves AHDA's exact MFA/PAM policy open, so each rule is configuration; the one fixed rule is the
/// minimum viable scope, R01, which start-up refuses to run without.
/// </summary>
public sealed class MultiFactorPolicy
{
    public const string Section = "Identity:Mfa";

    /// <summary>CTL-07's minimum scope: a System Administrator never holds a session without a second factor.</summary>
    public const string MinimumRequiredRole = "R01";

    /// <summary>
    /// Holding any of these roles requires MFA before a session is issued. Listing R01–R08 applies SCR-002 to every
    /// user. A user already enrolled is always challenged, whatever their roles, so an enrolment cannot be downgraded.
    /// </summary>
    public IReadOnlyList<string> RequiredRoles { get; set; } = [MinimumRequiredRole];

    /// <summary>
    /// Whether a person who has passed the first factor but has no enrolled second factor may enrol one there and then.
    /// When false, such a person cannot sign in until their factor is enrolled some other way (F-2).
    /// </summary>
    public bool AllowEnrolmentAtSignIn { get; set; } = true;

    /// <summary>
    /// ID token <c>amr</c> values (RFC 8176) that show AHDA's identity provider already applied a second factor, for
    /// example <c>mfa</c>, <c>otp</c> or <c>hwk</c>. Empty by default: the platform's MFA provider challenges every
    /// SSO user who requires MFA. An ID token counts only if it also carries <c>auth_time</c>.
    /// </summary>
    public IReadOnlyList<string> IdentityProviderMethods { get; set; } = [];

    public bool RequiresMultiFactor(IEnumerable<string> roleCodes) => roleCodes.Any(RequiredRoles.Contains);
}
