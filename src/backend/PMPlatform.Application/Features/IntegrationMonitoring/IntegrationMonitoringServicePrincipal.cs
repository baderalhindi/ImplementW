namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// The SERVICE principal (db/seed <c>svc.integration-monitoring</c>) that records telemetry, runs FG-05's operations and their retries,
/// evaluates health and raises alerts (TASK-075).
/// </summary>
public static class IntegrationMonitoringServicePrincipal
{
    public static readonly Guid Id = new("00000000-0000-4000-8000-0000000000f3");

    public const string Module = "IntegrationMonitoring";
}
