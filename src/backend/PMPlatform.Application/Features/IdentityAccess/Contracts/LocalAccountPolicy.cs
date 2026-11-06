namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// Local accounts (the 5 Nov 2026 review §6.1), bound from <c>Identity:LocalAccounts</c>. <see cref="Enabled"/> is the production switch: turned off once
/// Microsoft sign-in or Nafath is live, a local account signs in no more and none can be created.
/// </summary>
public sealed class LocalAccountPolicy
{
    public const string Section = "Identity:LocalAccounts";

    public bool Enabled { get; set; } = true;

    public int MaxFailedAttempts { get; set; } = 5;

    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan PasswordResetLinkLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>At most one reset e-mail per account in this time, whoever asks and from wherever.</summary>
    public TimeSpan PasswordResetCooldown { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// How many administrators a permission profile change needs (the 5 Nov 2026 review §6.2), bound from <c>Identity:ProfileApproval</c>: 3 (author, reviewer and
/// publisher, each another person who does not hold the profile) unless an environment says less; 1 lets one administrator prepare, verify and publish on a test environment.
/// </summary>
public sealed class ProfileApprovalPolicy
{
    public const string Section = "Identity:ProfileApproval";

    public int RequiredAdministrators { get; set; } = 3;
}
