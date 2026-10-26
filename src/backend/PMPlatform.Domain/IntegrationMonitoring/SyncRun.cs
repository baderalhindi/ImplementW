using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// One synchronisation or reconciliation run against an instance (Blueprint §17.1), with its counts. A reconciliation reads and
/// compares; it applies nothing, so <see cref="RecordsApplied"/> stays 0 and a mismatch raises an alert for investigation instead
/// of repairing the authoritative data (TASK-075). Delete policy: RETAIN.
/// </summary>
public sealed class SyncRun : AuditedEntity
{
    public Guid IntegrationInstanceId { get; set; }

    public SyncRunType RunType { get; set; }

    /// <summary>The correlation id of the lineage that ran it.</summary>
    public Guid CorrelationId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public SyncRunStatus Status { get; set; }

    public int RecordsRead { get; set; }

    public int RecordsApplied { get; set; }

    /// <summary>Records the source could not supply.</summary>
    public int RecordsRejected { get; set; }

    public int MismatchCount { get; set; }
}
