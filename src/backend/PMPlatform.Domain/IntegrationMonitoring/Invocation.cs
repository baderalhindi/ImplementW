using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IntegrationMonitoring;

/// <summary>
/// One attempt at one call to or from an integration (Blueprint §17.1 Invocation / Attempt). The attempts of one operation share
/// a lineage — the instance, <see cref="OperationCode"/> and <see cref="IdempotencyKey"/> — numbered by <see cref="AttemptNo"/>,
/// and keep the lineage's <see cref="CorrelationId"/> on every retry (DC-INT-05, DC-INT-08). An attempt never carries a secret or a
/// payload: <see cref="ErrorSummary"/> is the kind of fault only. Delete policy: RETAIN.
/// </summary>
public sealed class Invocation : AuditedEntity
{
    public Guid IntegrationInstanceId { get; set; }

    /// <summary>The run the attempt belongs to, for an operation that is a run.</summary>
    public Guid? SyncRunId { get; set; }

    /// <summary>What was called, e.g. <c>AUTHENTICATE</c>, <c>SEND</c>, <c>RECONCILE</c>.</summary>
    public required string OperationCode { get; set; }

    public Guid CorrelationId { get; set; }

    /// <summary>The lineage key: the same for every attempt of one logical operation, so a retry can never be a second operation.</summary>
    public string? IdempotencyKey { get; set; }

    public short AttemptNo { get; set; } = 1;

    /// <summary>The module that made the call: the producer of the telemetry, or IntegrationMonitoring for an operation it runs.</summary>
    public string? SourceModule { get; set; }

    /// <summary>A safe pointer to the business record, e.g. <c>NotificationDelivery/{id}</c> (BR-INT-031); never its content.</summary>
    public string? SourceReference { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public InvocationStatus Status { get; set; }

    /// <summary>Null when the attempt succeeded or is still in progress.</summary>
    public InvocationFailureCategory? FailureCategory { get; set; }

    /// <summary>A fixed code, e.g. <c>UNREACHABLE</c>, <c>REJECTED</c>; never the provider's own message.</summary>
    public string? ResponseCode { get; set; }

    /// <summary>The kind of fault, e.g. <c>SocketException</c>: redacted by construction.</summary>
    public string? ErrorSummary { get; set; }

    /// <summary>When the next attempt is due; set only on a RETRY_SCHEDULED attempt.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }
}
