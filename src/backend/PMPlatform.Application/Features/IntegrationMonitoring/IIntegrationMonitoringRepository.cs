using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// The <c>integration_monitoring</c> schema (TASK-075) — the only tables FG-05 writes. Nothing here reads or writes a business table:
/// integration health stays apart from every source's lifecycle state (Blueprint §17.1, BR-INT-002).
/// </summary>
public interface IIntegrationMonitoringRepository
{
    /// <summary>A transaction, or the caller's when one is open already (the outbox dispatcher's), which the caller commits.</summary>
    public Task<IIntegrationMonitoringWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Every definition, by code. Not tracked.</summary>
    public Task<IReadOnlyList<IntegrationDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken);

    /// <summary>Every instance, by definition and code. Not tracked.</summary>
    public Task<IReadOnlyList<IntegrationInstance>> ListInstancesAsync(CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<IntegrationDefinition?> FindDefinitionAsync(Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<IntegrationDefinition?> FindDefinitionByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>The definition's instance telemetry and FG-05's operations are attributed to: its enabled instance, the first by code. Not tracked.</summary>
    public Task<IntegrationInstance?> FindPrimaryInstanceAsync(Guid definitionId, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<IntegrationInstance?> FindInstanceAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>Each instance's id with its definition's code, for views that name the integration.</summary>
    public Task<IReadOnlyDictionary<Guid, string>> GetIntegrationCodesAsync(CancellationToken cancellationToken);

    /// <summary>Tracked and locked for the rest of the transaction: health is rewritten one recorder at a time per instance.</summary>
    public Task<IntegrationInstance?> LockInstanceAsync(Guid instanceId, CancellationToken cancellationToken);

    /// <summary>
    /// The instance a definition's telemetry is attributed to — its enabled instance, the first by code — tracked and locked. Telemetry
    /// names the integration, not the instance: an environment deploys each definition once.
    /// </summary>
    public Task<IntegrationInstance?> LockTelemetryInstanceAsync(Guid definitionId, CancellationToken cancellationToken);

    /// <summary>OPEN and ACKNOWLEDGED alerts per instance.</summary>
    public Task<IReadOnlyDictionary<Guid, int>> CountLiveAlertsAsync(CancellationToken cancellationToken);

    public Task<bool> InvocationExistsAsync(Guid invocationId, CancellationToken cancellationToken);

    /// <summary>The lineage's attempt with the highest number, tracked; null for a lineage not seen yet.</summary>
    public Task<Invocation?> FindLatestAttemptAsync(Guid instanceId, string operationCode, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Every attempt of the lineage, oldest first. Not tracked.</summary>
    public Task<IReadOnlyList<Invocation>> GetLineageAsync(Guid instanceId, string operationCode, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<Invocation?> FindInvocationAsync(Guid invocationId, CancellationToken cancellationToken);

    /// <summary>Tracked and locked for the rest of the transaction, waiting for another holder: two retries of one lineage go one after the other.</summary>
    public Task<Invocation?> LockInvocationAsync(Guid invocationId, CancellationToken cancellationToken);

    /// <summary>A page of attempts, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<Invocation> Items, int TotalCount)> SearchInvocationsAsync(InvocationQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>RETRY_SCHEDULED attempts of ACTIVE integrations whose next attempt is due, earliest first.</summary>
    public Task<IReadOnlyList<Guid>> FindDueRetryIdsAsync(DateTimeOffset now, int count, CancellationToken cancellationToken);

    /// <summary>The attempt, tracked and locked, if it is still RETRY_SCHEDULED and due; null if not, or if another pass holds it.</summary>
    public Task<Invocation?> ClaimDueRetryAsync(Guid invocationId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Attempts of <paramref name="sourceModule"/> still IN_PROGRESS since before <paramref name="startedBefore"/>: their process stopped.</summary>
    public Task<IReadOnlyList<Guid>> FindAbandonedAttemptIdsAsync(string sourceModule, DateTimeOffset startedBefore, int count, CancellationToken cancellationToken);

    /// <summary>The attempt, tracked and locked, if it is still IN_PROGRESS since before <paramref name="startedBefore"/>.</summary>
    public Task<Invocation?> ClaimAbandonedAttemptAsync(Guid invocationId, DateTimeOffset startedBefore, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<SyncRun?> FindSyncRunAsync(Guid syncRunId, CancellationToken cancellationToken);

    /// <summary>A page of runs, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<SyncRun> Items, int TotalCount)> SearchSyncRunsAsync(SyncRunQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The OPEN or ACKNOWLEDGED alert of the instance and type, including one added in this unit of work and not yet saved.</summary>
    public Task<OperationalAlert?> FindLiveAlertAsync(Guid instanceId, OperationalAlertType alertType, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<OperationalAlert?> FindAlertAsync(Guid alertId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>A page of alerts, newest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<OperationalAlert> Items, int TotalCount)> SearchAlertsAsync(OperationalAlertQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <summary>What the instance's attempts in the window add up to (ADR-004's measures).</summary>
    public Task<InvocationStatistics> MeasureAsync(Guid instanceId, DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken);

    public void Add(Invocation invocation);

    public void Add(SyncRun syncRun);

    public void Add(OperationalAlert alert);

    public uint RowVersionOf(IntegrationDefinition definition);

    public uint RowVersionOf(OperationalAlert alert);

    public Task<IntegrationMonitoringSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

public interface IIntegrationMonitoringWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum IntegrationMonitoringSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key another request took first: a lineage's attempt number, or an instance's live alert of a type.</summary>
    Duplicate = 3,
}

/// <summary>
/// What an instance's attempts in a window add up to, before any rate is taken. Durations are in milliseconds; a percentile or median
/// over no sample is null.
/// </summary>
public sealed record InvocationStatistics(
    int Attempts,
    int FailedAttempts,
    int DeliveredLineages,
    int UndeliveredLineages,
    double? MedianLatencyMilliseconds,
    double? P95LatencyMilliseconds,
    int Receipts,
    double? MedianReceiptLatencyMilliseconds);
