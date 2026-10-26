namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// Configuration section <c>Identity:Session</c>. The lifetimes are PROVISIONAL: session lifetime, idle timeout and
/// revocation behaviour are AHDA's values (ADR-007 Impact; control matrix G-2) and replace these when confirmed.
/// </summary>
internal sealed class SessionTokenOptions
{
    public const string Section = "Identity:Session";

    /// <summary>How long an access token is accepted. A disabled user or an ended assignment stops at the next refresh, so this bounds how long either can go unnoticed.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Idle timeout: a session not refreshed within this ends.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Absolute lifetime from sign-in; no refresh extends it.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>How long a person has, after the first factor, to pass the second (TASK-029). Provisional, like the others.</summary>
    public TimeSpan MultiFactorTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long a person has, after every factor, to complete Nafath's identity verification (TASK-068). Longer than a
    /// Nafath transaction (<c>Identity:Nafath:TransactionLifetime</c>, 10 min), so a verification Nafath could not complete
    /// can be started again without signing in again. Provisional, like the others.
    /// </summary>
    public TimeSpan IdentityVerificationTokenLifetime { get; set; } = TimeSpan.FromMinutes(20);
}
