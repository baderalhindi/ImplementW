namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>The category of an integration (FG-05 §4.1), for the catalogue and its filters.</summary>
public enum IntegrationKind
{
    /// <summary>Directory, single sign-on and MFA (FG-03).</summary>
    Identity = 1,

    /// <summary>E-mail and SMS (WF-15).</summary>
    Messaging = 2,

    /// <summary>Identity proofing: Nafath.</summary>
    Verification = 3,

    /// <summary>Financial sources (WF-14).</summary>
    Finance = 4,

    /// <summary>SIEM and malware scanning.</summary>
    Security = 5,

    /// <summary>Internal AHDA systems and other data sources.</summary>
    Data = 6,
}
