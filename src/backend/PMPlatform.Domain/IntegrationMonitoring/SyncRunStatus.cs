namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>A sync run's outcome (ERD §6). A run ends once and is then fixed.</summary>
public enum SyncRunStatus
{
    Running = 1,

    /// <summary>Every record was read and compared.</summary>
    Succeeded = 2,

    /// <summary>Some records could not be read; the counts say how many.</summary>
    Partial = 3,

    /// <summary>The source could not be reached; nothing was compared.</summary>
    Failed = 4,
}
