namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>How urgent an operational alert is. Fixed per alert type; AHDA's own levels are TBC-INT-17.</summary>
public enum OperationalAlertSeverity
{
    Info = 1,

    Warning = 2,

    Critical = 3,
}
