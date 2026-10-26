namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>Which way an integration's data flows, seen from the platform (FG-05 §4.2).</summary>
public enum IntegrationDirection
{
    /// <summary>Data comes in: a directory, an identity provider, a source system. A failure tends to show as stale data.</summary>
    Inbound = 1,

    /// <summary>Data goes out: a mail relay, an SMS gateway, a verification request. A failure shows as an undelivered message.</summary>
    Outbound = 2,

    /// <summary>Both, each direction monitored on its own.</summary>
    Bidirectional = 3,
}
