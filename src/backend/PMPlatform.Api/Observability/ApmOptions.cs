namespace PMPlatform.Api.Observability;

/// <summary>
/// The APM and error-tracking settings (TASK-090). <c>APM_DSN</c> (Secret, from the secret store in SIT/UAT/PROD) names the
/// ingestion endpoint; without it nothing is captured or sent. <c>APM_ENVIRONMENT_TAG</c> labels every event so the dashboard
/// filters by environment; it is required whenever a DSN is set. The rest is <c>Apm:*</c> in appsettings.json.
/// </summary>
internal sealed class ApmOptions
{
    public const string DsnVariable = "APM_DSN";
    public const string EnvironmentTagVariable = "APM_ENVIRONMENT_TAG";
    public const string Section = "Apm";

    /// <summary>The environment tag of production, where the diagnostic exception endpoint may never be enabled.</summary>
    public const string ProductionTag = "prod";

    public string? Dsn { get; set; }

    public string? EnvironmentTag { get; set; }

    /// <summary>The share of requests traced for latency and throughput, 0 to 1. Errors are always captured.</summary>
    public double TracesSampleRate { get; set; } = 1.0;

    /// <summary>
    /// Maps <c>POST /api/v1/apm-diagnostic-exceptions</c>, which throws on purpose, for the validation check: "throw a test exception
    /// in a non-PROD environment". Off unless an environment turns it on; refused at start-up in production.
    /// </summary>
    public bool DiagnosticExceptionEndpoint { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(Dsn);

    public bool IsValid() =>
        TracesSampleRate is >= 0 and <= 1
        && (!Enabled || !string.IsNullOrWhiteSpace(EnvironmentTag))
        && (!DiagnosticExceptionEndpoint
            || (!string.IsNullOrWhiteSpace(EnvironmentTag) && !string.Equals(EnvironmentTag, ProductionTag, StringComparison.OrdinalIgnoreCase)));
}
