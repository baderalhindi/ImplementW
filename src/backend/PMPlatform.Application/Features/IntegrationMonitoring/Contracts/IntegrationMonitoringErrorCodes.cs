namespace PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

/// <summary>FG-05's module codes (api-conventions R-27), from FG-05 §23 where it names one.</summary>
public static class IntegrationMonitoringErrorCodes
{
    /// <summary>422 (ERR-INT-005): FG-05 runs nothing for a disabled or retired integration.</summary>
    public const string IntegrationDisabled = "INTEGRATION_DISABLED";

    /// <summary>409 (ERR-INT-021): the attempt is not its lineage's latest, or its lineage is neither dead-lettered nor waiting for a retry.</summary>
    public const string InvocationNotRetryable = "INVOCATION_NOT_RETRYABLE";

    /// <summary>409 (ERR-INT-022): the lineage already succeeded; another attempt could repeat its effect (INT-CC-16).</summary>
    public const string InvocationAlreadySucceeded = "INVOCATION_ALREADY_SUCCEEDED";

    /// <summary>422 (ERR-INT-024): the attempt was observed, not run by FG-05; its own module retries it (e.g. WF-15's redrive).</summary>
    public const string RetryNotPermitted = "RETRY_NOT_PERMITTED";

    /// <summary>422 (ERR-INT-043): the integration has no reconciliation.</summary>
    public const string ReconciliationNotAvailable = "RECONCILIATION_NOT_AVAILABLE";
}
