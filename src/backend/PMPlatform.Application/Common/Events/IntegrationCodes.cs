namespace PMPlatform.Application.Common.Events;

/// <summary>
/// The integration definitions' codes (db/seed), by which a producer names the integration an
/// <see cref="IntegrationInvocationData"/> is about. Shared vocabulary, as FG-04's family codes are: no producer references
/// IntegrationMonitoring to name one.
/// </summary>
public static class IntegrationCodes
{
    /// <summary>AHDA's directory over LDAP (<c>AD_LDAP_URL</c>).</summary>
    public const string Directory = "DIRECTORY";

    /// <summary>AHDA's identity provider over OpenID Connect (<c>SSO_OIDC_AUTHORITY</c>).</summary>
    public const string SingleSignOn = "SSO";

    /// <summary>The MFA verification provider (<c>MFA_PROVIDER_ENDPOINT</c>).</summary>
    public const string MultiFactor = "MFA";

    /// <summary>Nafath, the national identity verification service (<c>NAFATH_*</c>).</summary>
    public const string Nafath = "NAFATH";

    /// <summary>AHDA's Exchange SMTP relay (<c>EXCHANGE_SMTP_HOST</c>).</summary>
    public const string ExchangeEmail = "EXCHANGE_EMAIL";

    /// <summary>The SMS provider (ADR-004; <c>SMS_*</c>, TASK-103).</summary>
    public const string Sms = "SMS";
}
