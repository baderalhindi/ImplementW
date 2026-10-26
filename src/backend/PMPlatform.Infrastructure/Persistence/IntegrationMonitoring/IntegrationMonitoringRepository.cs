using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.IntegrationMonitoring;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IntegrationMonitoring;

/// <summary>The <c>integration_monitoring</c> schema (TASK-075). Every query here reads or writes that schema and no other.</summary>
internal sealed class IntegrationMonitoringRepository(PMPlatformDbContext context) : IIntegrationMonitoringRepository
{
    public async Task<IIntegrationMonitoringWork> BeginAsync(CancellationToken cancellationToken) =>
        context.Database.CurrentTransaction is null
            ? new IntegrationMonitoringWork(await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
            : new IntegrationMonitoringWork(null);

    public async Task<IReadOnlyList<IntegrationDefinition>> ListDefinitionsAsync(CancellationToken cancellationToken) =>
        await context.Set<IntegrationDefinition>().AsNoTracking().OrderBy(d => d.Code).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<IntegrationInstance>> ListInstancesAsync(CancellationToken cancellationToken) =>
        await context.Set<IntegrationInstance>().AsNoTracking()
            .OrderBy(i => i.IntegrationDefinitionId).ThenBy(i => i.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IntegrationDefinition?> FindDefinitionAsync(Guid definitionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        IntegrationDefinition? definition = await context.Set<IntegrationDefinition>().SingleOrDefaultAsync(d => d.Id == definitionId, cancellationToken).ConfigureAwait(false);
        if (definition is not null)
        {
            AdministrationPersistence.ExpectVersion(context, definition, expectedVersion);
        }

        return definition;
    }

    public Task<IntegrationDefinition?> FindDefinitionByCodeAsync(string code, CancellationToken cancellationToken) =>
        context.Set<IntegrationDefinition>().AsNoTracking().SingleOrDefaultAsync(d => d.Code == code, cancellationToken);

    public Task<IntegrationInstance?> FindPrimaryInstanceAsync(Guid definitionId, CancellationToken cancellationToken) =>
        context.Set<IntegrationInstance>().AsNoTracking()
            .Where(i => i.IntegrationDefinitionId == definitionId)
            .OrderByDescending(i => i.IsEnabled).ThenBy(i => i.Code)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<IntegrationInstance?> FindInstanceAsync(Guid instanceId, CancellationToken cancellationToken) =>
        context.Set<IntegrationInstance>().AsNoTracking().SingleOrDefaultAsync(i => i.Id == instanceId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetIntegrationCodesAsync(CancellationToken cancellationToken) =>
        await (from instance in context.Set<IntegrationInstance>().AsNoTracking()
               join definition in context.Set<IntegrationDefinition>() on instance.IntegrationDefinitionId equals definition.Id
               select new { instance.Id, definition.Code })
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken).ConfigureAwait(false);

    public async Task<IntegrationInstance?> LockInstanceAsync(Guid instanceId, CancellationToken cancellationToken) =>
        (await Untracked<IntegrationInstance>(instanceId).Set<IntegrationInstance>()
                .FromSql($"SELECT *, xmin FROM integration_monitoring.integration_instance WHERE id = {instanceId} FOR UPDATE")
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();

    public async Task<IntegrationInstance?> LockTelemetryInstanceAsync(Guid definitionId, CancellationToken cancellationToken) =>
        await FindPrimaryInstanceAsync(definitionId, cancellationToken).ConfigureAwait(false) is { } instance
            ? await LockInstanceAsync(instance.Id, cancellationToken).ConfigureAwait(false)
            : null;

    public async Task<IReadOnlyDictionary<Guid, int>> CountLiveAlertsAsync(CancellationToken cancellationToken) =>
        await context.Set<OperationalAlert>().AsNoTracking()
            .Where(a => a.Status == OperationalAlertStatus.Open || a.Status == OperationalAlertStatus.Acknowledged)
            .GroupBy(a => a.IntegrationInstanceId)
            .Select(g => new { InstanceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.InstanceId, x => x.Count, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, IntegrationQueueState>> MeasureQueuesAsync(CancellationToken cancellationToken) =>
        (await context.Database.SqlQuery<QueueRow>($"""
                SELECT q.integration_instance_id,
                       (count(*) FILTER (WHERE q.status = 'IN_PROGRESS'))::int AS in_flight,
                       (count(*) FILTER (WHERE q.status = 'RETRY_SCHEDULED'))::int AS awaiting_retry,
                       min(COALESCE(f.started_at, q.started_at)) FILTER (WHERE q.status IN ('IN_PROGRESS', 'RETRY_SCHEDULED')) AS oldest_queued_at,
                       (count(*) FILTER (WHERE q.status = 'DEAD_LETTER'))::int AS dead_letters
                FROM integration_monitoring.invocation q
                LEFT JOIN integration_monitoring.invocation f
                       ON f.integration_instance_id = q.integration_instance_id AND f.operation_code = q.operation_code
                      AND f.idempotency_key = q.idempotency_key AND f.attempt_no = 1
                WHERE q.status IN ('IN_PROGRESS', 'RETRY_SCHEDULED', 'DEAD_LETTER')
                GROUP BY q.integration_instance_id
                """).ToListAsync(cancellationToken).ConfigureAwait(false))
        .ToDictionary(r => r.IntegrationInstanceId, r => new IntegrationQueueState(r.InFlight, r.AwaitingRetry, r.OldestQueuedAt, r.DeadLetters));

    public Task<bool> InvocationExistsAsync(Guid invocationId, CancellationToken cancellationToken) =>
        context.Set<Invocation>().AnyAsync(i => i.Id == invocationId, cancellationToken);

    public Task<Invocation?> FindLatestAttemptAsync(Guid instanceId, string operationCode, string idempotencyKey, CancellationToken cancellationToken) =>
        context.Set<Invocation>()
            .Where(i => i.IntegrationInstanceId == instanceId && i.OperationCode == operationCode && i.IdempotencyKey == idempotencyKey)
            .OrderByDescending(i => i.AttemptNo)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Invocation>> GetLineageAsync(Guid instanceId, string operationCode, string idempotencyKey, CancellationToken cancellationToken) =>
        await context.Set<Invocation>().AsNoTracking()
            .Where(i => i.IntegrationInstanceId == instanceId && i.OperationCode == operationCode && i.IdempotencyKey == idempotencyKey)
            .OrderBy(i => i.AttemptNo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Invocation?> FindInvocationAsync(Guid invocationId, CancellationToken cancellationToken) =>
        context.Set<Invocation>().SingleOrDefaultAsync(i => i.Id == invocationId, cancellationToken);

    public async Task<Invocation?> LockInvocationAsync(Guid invocationId, CancellationToken cancellationToken) =>
        (await Untracked<Invocation>(invocationId).Set<Invocation>()
                .FromSql($"SELECT * FROM integration_monitoring.invocation WHERE id = {invocationId} FOR UPDATE")
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();

    public Task<(IReadOnlyList<Invocation> Items, int TotalCount)> SearchInvocationsAsync(InvocationQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<Invocation> rows = context.Set<Invocation>().AsNoTracking();
        if (query.IntegrationId is { } integrationId)
        {
            rows = rows.Where(i => context.Set<IntegrationInstance>().Any(x => x.Id == i.IntegrationInstanceId && x.IntegrationDefinitionId == integrationId));
        }

        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(i => query.Statuses.Contains(i.Status));
        }

        if (query.OperationCode is { } operation)
        {
            rows = rows.Where(i => i.OperationCode == operation);
        }

        if (query.CorrelationId is { } correlationId)
        {
            rows = rows.Where(i => i.CorrelationId == correlationId);
        }

        if (query.IdempotencyKey is { } key)
        {
            rows = rows.Where(i => i.IdempotencyKey == key);
        }

        if (query.SourceReference is { } reference)
        {
            rows = rows.Where(i => i.SourceReference == reference);
        }

        if (query.StartedFrom is { } from)
        {
            rows = rows.Where(i => i.StartedAt >= from);
        }

        if (query.StartedTo is { } to)
        {
            rows = rows.Where(i => i.StartedAt < to);
        }

        return PageAsync(rows.OrderByDescending(i => i.StartedAt).ThenByDescending(i => i.AttemptNo).ThenByDescending(i => i.Id), page, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> FindDueRetryIdsAsync(DateTimeOffset now, int count, CancellationToken cancellationToken) =>
        await (from invocation in context.Set<Invocation>().AsNoTracking()
               join instance in context.Set<IntegrationInstance>() on invocation.IntegrationInstanceId equals instance.Id
               join definition in context.Set<IntegrationDefinition>() on instance.IntegrationDefinitionId equals definition.Id
               where invocation.Status == InvocationStatus.RetryScheduled && invocation.NextAttemptAt <= now && definition.Status == IntegrationDefinitionStatus.Active
               orderby invocation.NextAttemptAt, invocation.Id
               select invocation.Id)
            .Take(count)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<Invocation?> ClaimDueRetryAsync(Guid invocationId, DateTimeOffset now, CancellationToken cancellationToken) =>
        (await Untracked<Invocation>(invocationId).Set<Invocation>()
                .FromSql($"""
                    SELECT * FROM integration_monitoring.invocation
                    WHERE id = {invocationId} AND status = 'RETRY_SCHEDULED' AND next_attempt_at <= {now}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();

    public async Task<IReadOnlyList<Guid>> FindAbandonedAttemptIdsAsync(string sourceModule, DateTimeOffset startedBefore, int count, CancellationToken cancellationToken) =>
        await context.Set<Invocation>().AsNoTracking()
            .Where(i => i.Status == InvocationStatus.InProgress && i.SourceModule == sourceModule && i.StartedAt < startedBefore)
            .OrderBy(i => i.StartedAt).ThenBy(i => i.Id)
            .Select(i => i.Id)
            .Take(count)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<Invocation?> ClaimAbandonedAttemptAsync(Guid invocationId, DateTimeOffset startedBefore, CancellationToken cancellationToken) =>
        (await Untracked<Invocation>(invocationId).Set<Invocation>()
                .FromSql($"""
                    SELECT * FROM integration_monitoring.invocation
                    WHERE id = {invocationId} AND status = 'IN_PROGRESS' AND started_at < {startedBefore}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .SingleOrDefault();

    public Task<SyncRun?> FindSyncRunAsync(Guid syncRunId, CancellationToken cancellationToken) =>
        context.Set<SyncRun>().SingleOrDefaultAsync(r => r.Id == syncRunId, cancellationToken);

    public Task<(IReadOnlyList<SyncRun> Items, int TotalCount)> SearchSyncRunsAsync(SyncRunQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<SyncRun> rows = context.Set<SyncRun>().AsNoTracking();
        if (query.IntegrationId is { } integrationId)
        {
            rows = rows.Where(r => context.Set<IntegrationInstance>().Any(x => x.Id == r.IntegrationInstanceId && x.IntegrationDefinitionId == integrationId));
        }

        if (query.RunTypes.Count > 0)
        {
            rows = rows.Where(r => query.RunTypes.Contains(r.RunType));
        }

        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(r => query.Statuses.Contains(r.Status));
        }

        if (query.CorrelationId is { } correlationId)
        {
            rows = rows.Where(r => r.CorrelationId == correlationId);
        }

        return PageAsync(rows.OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id), page, cancellationToken);
    }

    public async Task<OperationalAlert?> FindLiveAlertAsync(Guid instanceId, OperationalAlertType alertType, CancellationToken cancellationToken) =>
        context.Set<OperationalAlert>().Local.FirstOrDefault(a => IsLive(a) && a.IntegrationInstanceId == instanceId && a.AlertType == alertType)
        ?? await context.Set<OperationalAlert>()
            .Where(a => a.IntegrationInstanceId == instanceId && a.AlertType == alertType
                        && (a.Status == OperationalAlertStatus.Open || a.Status == OperationalAlertStatus.Acknowledged))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> AlertRaisedSinceAsync(Guid instanceId, OperationalAlertType alertType, DateTimeOffset since, CancellationToken cancellationToken) =>
        context.Set<OperationalAlert>().AnyAsync(a => a.IntegrationInstanceId == instanceId && a.AlertType == alertType && a.RaisedAt >= since, cancellationToken);

    public async Task<OperationalAlert?> FindAlertAsync(Guid alertId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        OperationalAlert? alert = await context.Set<OperationalAlert>().SingleOrDefaultAsync(a => a.Id == alertId, cancellationToken).ConfigureAwait(false);
        if (alert is not null)
        {
            AdministrationPersistence.ExpectVersion(context, alert, expectedVersion);
        }

        return alert;
    }

    public Task<(IReadOnlyList<OperationalAlert> Items, int TotalCount)> SearchAlertsAsync(OperationalAlertQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        IQueryable<OperationalAlert> rows = context.Set<OperationalAlert>().AsNoTracking();
        if (query.IntegrationId is { } integrationId)
        {
            rows = rows.Where(a => context.Set<IntegrationInstance>().Any(x => x.Id == a.IntegrationInstanceId && x.IntegrationDefinitionId == integrationId));
        }

        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(a => query.Statuses.Contains(a.Status));
        }

        if (query.AlertTypes.Count > 0)
        {
            rows = rows.Where(a => query.AlertTypes.Contains(a.AlertType));
        }

        if (query.RaisedFrom is { } from)
        {
            rows = rows.Where(a => a.RaisedAt >= from);
        }

        if (query.RaisedTo is { } to)
        {
            rows = rows.Where(a => a.RaisedAt < to);
        }

        return PageAsync(rows.OrderByDescending(a => a.RaisedAt).ThenByDescending(a => a.Id), page, cancellationToken);
    }

    public async Task<InvocationStatistics> MeasureAsync(Guid instanceId, DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken)
    {
        string receipt = IntegrationMetricsCalculator.DeliveryReceiptOperation;
        StatisticsRow row = (await context.Database.SqlQuery<StatisticsRow>($"""
                WITH attempts AS (
                    SELECT * FROM integration_monitoring.invocation
                    WHERE integration_instance_id = {instanceId} AND started_at >= {windowStart} AND started_at < {windowEnd}
                      AND status <> 'IN_PROGRESS' AND operation_code <> {receipt}
                ), lineages AS (
                    SELECT DISTINCT ON (operation_code, idempotency_key) status
                    FROM attempts WHERE idempotency_key IS NOT NULL
                    ORDER BY operation_code, idempotency_key, attempt_no DESC
                ), receipts AS (
                    SELECT EXTRACT(EPOCH FROM (r.completed_at - s.started_at)) * 1000 AS latency
                    FROM integration_monitoring.invocation r
                    JOIN LATERAL (
                        SELECT min(a.started_at) AS started_at FROM integration_monitoring.invocation a
                        WHERE a.integration_instance_id = r.integration_instance_id AND a.idempotency_key = r.idempotency_key AND a.operation_code <> {receipt}
                    ) s ON s.started_at IS NOT NULL
                    WHERE r.integration_instance_id = {instanceId} AND r.operation_code = {receipt} AND r.status = 'SUCCEEDED'
                      AND r.started_at >= {windowStart} AND r.started_at < {windowEnd}
                )
                SELECT
                    (SELECT count(*) FROM attempts)::int AS attempts,
                    (SELECT count(*) FROM attempts WHERE status <> 'SUCCEEDED')::int AS failed_attempts,
                    (SELECT count(*) FROM lineages WHERE status = 'SUCCEEDED')::int AS delivered_lineages,
                    (SELECT count(*) FROM lineages WHERE status IN ('FAILED', 'DEAD_LETTER'))::int AS undelivered_lineages,
                    (SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (completed_at - started_at)) * 1000) FROM attempts)::float8 AS median_latency,
                    (SELECT percentile_cont(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (completed_at - started_at)) * 1000) FROM attempts)::float8 AS p95latency,
                    (SELECT count(*) FROM receipts)::int AS receipts,
                    (SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY latency) FROM receipts)::float8 AS median_receipt_latency
                """).ToListAsync(cancellationToken).ConfigureAwait(false)).Single();
        return new InvocationStatistics(
            row.Attempts, row.FailedAttempts, row.DeliveredLineages, row.UndeliveredLineages, row.MedianLatency, row.P95Latency, row.Receipts, row.MedianReceiptLatency);
    }

    public void Add(Invocation invocation) => context.Add(invocation);

    public void Add(SyncRun syncRun) => context.Add(syncRun);

    public void Add(OperationalAlert alert) => context.Add(alert);

    public uint RowVersionOf(IntegrationDefinition definition) => context.Entry(definition).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public uint RowVersionOf(OperationalAlert alert) => context.Entry(alert).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public async Task<IntegrationMonitoringSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return IntegrationMonitoringSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return IntegrationMonitoringSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A lineage's attempt number or an instance's live alert, taken by another request first.
            context.ChangeTracker.Clear();
            return IntegrationMonitoringSaveOutcome.Duplicate;
        }
    }

    /// <summary>
    /// The context with no tracked copy of the row, so a locking query materialises the row as it is under the lock: a query never refreshes
    /// an entity the context already tracks, and an earlier pass in the same scope may have tracked this one.
    /// </summary>
    private PMPlatformDbContext Untracked<TEntity>(Guid id)
        where TEntity : Domain.Common.AuditedEntity
    {
        if (context.Set<TEntity>().Local.FirstOrDefault(e => e.Id == id) is { } tracked)
        {
            context.Entry(tracked).State = EntityState.Detached;
        }

        return context;
    }

    private static bool IsLive(OperationalAlert alert) => alert.Status is OperationalAlertStatus.Open or OperationalAlertStatus.Acknowledged;

    private static async Task<(IReadOnlyList<T> Items, int TotalCount)> PageAsync<T>(IQueryable<T> rows, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<T> items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return (items, total);
    }

    /// <summary>Owns its transaction when it began one; joined to the caller's otherwise — the outbox dispatcher's — which the caller commits.</summary>
    private sealed class IntegrationMonitoringWork(IDbContextTransaction? transaction) : IIntegrationMonitoringWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    /// <summary>The statistics query's row; its columns are named in snake case, as the model's naming convention reads them.</summary>
    private sealed class StatisticsRow
    {
        public int Attempts { get; init; }

        public int FailedAttempts { get; init; }

        public int DeliveredLineages { get; init; }

        public int UndeliveredLineages { get; init; }

        public double? MedianLatency { get; init; }

        public double? P95Latency { get; init; }

        public int Receipts { get; init; }

        public double? MedianReceiptLatency { get; init; }
    }

    private sealed class QueueRow
    {
        public Guid IntegrationInstanceId { get; init; }

        public int InFlight { get; init; }

        public int AwaitingRetry { get; init; }

        public DateTimeOffset? OldestQueuedAt { get; init; }

        public int DeadLetters { get; init; }
    }
}
