namespace PMPlatform.Api.Middleware;

/// <summary>
/// The rate-limited endpoint classes (TASK-078, CTL-12). Each has its own counter and its own limit under
/// <c>RateLimiting:Policies</c>. A signed-in caller is counted by user, anyone else by client address.
/// </summary>
internal enum RateLimitPolicy
{
    /// <summary>Every endpoint that verifies a credential or a second factor, or starts doing so: password, SSO, MFA, Nafath, step-up.</summary>
    SignIn,

    /// <summary>The refresh-token exchange. Each open SPA tab calls it once per access-token lifetime, so it is counted apart from sign-in.</summary>
    SessionRefresh,

    /// <summary>Every sensitive write (api-conventions R-35): the operations that change business, financial, identity or access state.</summary>
    SensitiveWrite,

    /// <summary>The SPA's error reports (TASK-090). Anonymous, so counted by client address; a crashing screen reports once per error.</summary>
    ClientErrorReport,
}

/// <summary>Puts an action under a <see cref="RateLimitPolicy"/>. A <c>[SensitiveWrite]</c> action is under <see cref="RateLimitPolicy.SensitiveWrite"/> without it.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RateLimitedAttribute(RateLimitPolicy policy) : Attribute
{
    public RateLimitPolicy Policy { get; } = policy;
}

/// <summary>A policy's limit: at most <see cref="PermitLimit"/> requests per client in a window of <see cref="Window"/>.</summary>
internal sealed class RateLimitRule
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }
}

/// <summary>
/// <c>RateLimiting:Policies</c>: one rule per <see cref="RateLimitPolicy"/>. The thresholds are delivery-team engineering values
/// (TASK-004 §7), set in appsettings.json and overridable per environment like any other setting; every policy must have one,
/// or the API does not start.
/// </summary>
internal sealed class RateLimitingOptions
{
    public const string Section = "RateLimiting";

    public Dictionary<RateLimitPolicy, RateLimitRule> Policies { get; } = [];

    public bool IsComplete() => Enum.GetValues<RateLimitPolicy>().All(policy =>
        Policies.TryGetValue(policy, out RateLimitRule? rule) && rule.PermitLimit > 0 && rule.Window > TimeSpan.Zero);
}
