namespace PMPlatform.Api.Authorization;

/// <summary>
/// Role-based policies over the session's role claims. A stop-gap for the screens that exist before the
/// Section 10.1 authorization engine (TASK-030), which replaces them with permission checks.
/// </summary>
internal static class AuthorizationPolicies
{
    /// <summary>ADM-041 and the other FG-03 administration screens: R01 System Administrator.</summary>
    public const string SystemAdministrator = nameof(SystemAdministrator);

    public const string SystemAdministratorRole = "R01";
}
