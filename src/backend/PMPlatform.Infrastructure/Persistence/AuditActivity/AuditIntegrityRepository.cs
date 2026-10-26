using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.AuditActivity;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>
/// The hash chain as integrity verification reads it, and the audit pipeline's state (TASK-073). Each link's hash is recomputed by
/// the same database function the chain trigger used to set it (<c>audit_activity.audit_event_hash</c>), from the row as it is now.
/// </summary>
internal sealed class AuditIntegrityRepository(PMPlatformDbContext context) : IAuditIntegrityRepository
{
    public async Task<IReadOnlyList<AuditChainLink>> ReadChainAsync(AuditStorePosition? after, int count, CancellationToken cancellationToken)
    {
        DbConnection connection = context.Database.GetDbConnection();
        bool opened = connection.State != ConnectionState.Open;
        if (opened)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT e.id, e.recorded_at, e.event_hash, e.previous_event_hash, audit_activity.audit_event_hash(e), e.attribute_digest
                FROM audit_activity.audit_event e
                {(after is null ? string.Empty : "WHERE e.recorded_at > @after")}
                ORDER BY e.recorded_at
                LIMIT @count
                """;
            if (after is not null)
            {
                AddParameter(command, "after", after.RecordedAt);
            }

            AddParameter(command, "count", count);

            List<AuditChainLink> links = [];
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                links.Add(new AuditChainLink(
                    reader.GetGuid(0),
                    reader.GetFieldValue<DateTimeOffset>(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5)));
            }

            return links;
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<IReadOnlyList<AuditEventAttribute>> ReadAttributesAsync(IReadOnlyCollection<Guid> auditEventIds, CancellationToken cancellationToken) =>
        await context.Set<AuditEventAttribute>().AsNoTracking().Where(a => auditEventIds.Contains(a.AuditEventId)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<AuditIngestionStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        IQueryable<OutboxMessage> pending = context.Set<OutboxMessage>().AsNoTracking().Where(m => m.MessageType == EventKind.AuditEvent && m.DispatchedAt == null);
        int pendingCount = await pending.CountAsync(cancellationToken).ConfigureAwait(false);
        int failingCount = await pending.CountAsync(m => m.LastError != null, cancellationToken).ConfigureAwait(false);
        DateTimeOffset? oldest = await pending.MinAsync(m => (DateTimeOffset?)m.OccurredAt, cancellationToken).ConfigureAwait(false);
        string? lastError = await pending.Where(m => m.LastError != null).OrderByDescending(m => m.UpdatedAt).Select(m => m.LastError).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<AuditEvent> events = context.Set<AuditEvent>().AsNoTracking();
        int stored = await events.CountAsync(cancellationToken).ConfigureAwait(false);
        DateTimeOffset? head = await events.MaxAsync(e => (DateTimeOffset?)e.RecordedAt, cancellationToken).ConfigureAwait(false);
        DateTimeOffset? checkpoint = await context.Set<ActivityProjectionCheckpoint>().AsNoTracking()
            .Where(c => c.Id == ActivityProjectionCheckpoint.SingletonId)
            .Select(c => c.LastRecordedAt)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        int lag = checkpoint is { } projected
            ? await events.CountAsync(e => e.RecordedAt > projected, cancellationToken).ConfigureAwait(false)
            : stored;

        return new AuditIngestionStatus(pendingCount, failingCount, oldest, lastError, stored, head, lag, checkpoint);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
