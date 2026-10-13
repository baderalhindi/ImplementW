using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// An operation FG-05 runs itself against an integration, under the definition's retry policy (Blueprint §17.1: Invocation → retry /
/// dead-letter / reconciliation → Operational Alert). Each attempt receives the lineage's key and correlation id, so an attempt that
/// reaches a provider twice reaches it as the same operation (FG-05 §7.6). An operation never writes another module's data: it reads,
/// compares and reports (BR-INT-002).
/// </summary>
internal interface IIntegrationOperation
{
    public string IntegrationCode { get; }

    public string OperationCode { get; }

    /// <summary>The run each attempt is, for an operation that is a synchronisation or reconciliation run; null otherwise.</summary>
    public SyncRunType? RunType { get; }

    /// <summary>Whether the integration it needs is configured in this environment; an operation that cannot run is not started.</summary>
    public bool IsAvailable { get; }

    /// <summary>One attempt. A technical failure is returned, not thrown; a throw is taken as one.</summary>
    public Task<IntegrationOperationResult> ExecuteAsync(IntegrationOperationContext context, CancellationToken cancellationToken);
}

/// <summary>What an attempt knows of itself: its lineage's key and correlation id, its number, and its run.</summary>
internal sealed record IntegrationOperationContext(Guid InvocationId, Guid? SyncRunId, Guid CorrelationId, string IdempotencyKey, short AttemptNo);

/// <summary>How an attempt ended, with its run's counts for an operation that is a run.</summary>
internal sealed record IntegrationOperationResult(
    InvocationFailureCategory? FailureCategory, string? ResponseCode, string? ErrorSummary, IntegrationRunOutcome? Run)
{
    public bool Succeeded => FailureCategory is null;

    public static IntegrationOperationResult Success(IntegrationRunOutcome? run = null) => new(null, null, null, run);

    public static IntegrationOperationResult Failure(InvocationFailureCategory category, string responseCode, IntegrationRunOutcome? run = null) =>
        new(category, responseCode, null, run);

    /// <summary>The operation threw: a technical failure, recorded as the kind of fault only.</summary>
    public static IntegrationOperationResult Faulted(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new IntegrationOperationResult(InvocationFailureCategory.Technical, "FAULTED", exception.GetType().Name, null);
    }
}

/// <summary>A run's counts. <see cref="MismatchDetail"/> names the records and attributes that differ, never their values.</summary>
internal sealed record IntegrationRunOutcome(SyncRunStatus Status, int RecordsRead, int RecordsApplied, int RecordsRejected, int MismatchCount, string? MismatchDetail);
