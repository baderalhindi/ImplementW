namespace PMPlatform.Api.Authorization;

/// <summary>
/// Configuration section <c>Identity:StepUp</c> (TASK-029, ADR-010): which operations need a fresh authentication
/// context, and how fresh. ADR-010 fixes that privileged and sensitive actions step up; which ones follows the
/// classification taxonomy still outstanding with AHDA Cybersecurity (UGV-01), so the list is configuration.
/// </summary>
internal sealed class StepUpOptions
{
    public const string Section = "Identity:StepUp";

    /// <summary>The longest time since the session's last second factor that a step-up operation accepts. Provisional (PTBC-027).</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Endpoint names (OpenAPI operation ids, e.g. <c>IdentityAccess_TestIdentityIntegration</c>) that require step-up.
    /// Start-up fails on a name no endpoint has, so a typing error cannot silently switch step-up off.
    /// </summary>
    public IReadOnlyList<string> Operations { get; set; } = [];
}
