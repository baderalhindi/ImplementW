namespace PMPlatform.Application.Common.Observability;

/// <summary>
/// Which probe a health check answers to (TASK-091, CTL-31). Infrastructure tags each check it registers; the API maps one endpoint per
/// tag (<c>PMPlatform.Api/HealthChecks/HealthEndpoints.cs</c>).
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    /// The process should be restarted when this fails. It never depends on the database or any other service: an outage elsewhere would
    /// otherwise restart every instance at once, and a restart does not bring the database back.
    /// </summary>
    public const string Liveness = "live";

    /// <summary>The instance can serve requests: the database answers. Uptime monitoring and the start-up probe read this.</summary>
    public const string Readiness = "ready";
}
