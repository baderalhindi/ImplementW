namespace PMPlatform.Infrastructure.Identity;

/// <summary>Configuration section <c>Identity:Mfa:Provider</c>: how the platform reaches the MFA provider.</summary>
internal sealed class MultiFactorProviderOptions
{
    public const string Section = "Identity:Mfa:Provider";

    /// <summary>The provider only over HTTPS. Cleared only by the tests' in-process MFA provider.</summary>
    public bool RequireHttps { get; set; } = true;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
