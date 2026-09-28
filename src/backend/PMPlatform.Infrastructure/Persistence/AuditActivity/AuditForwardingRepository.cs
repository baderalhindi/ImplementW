using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>The SIEM forwarder's queue over <c>audit_forwarding_record</c> (TASK-033). Only the forwarding record is tracked; the event is read-only.</summary>
internal sealed class AuditForwardingRepository(PMPlatformDbContext context) : IAuditForwardingRepository
{
    private static readonly AuditForwardingStatus[] Unforwarded = [AuditForwardingStatus.Pending, AuditForwardingStatus.Failed];

    public async Task<IReadOnlyList<PendingForwarding>> FindUnforwardedAsync(int count, CancellationToken cancellationToken)
    {
        List<AuditForwardingRecord> records = await context.Set<AuditForwardingRecord>()
            .Where(r => Unforwarded.Contains(r.Status))
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (records.Count == 0)
        {
            return [];
        }

        List<Guid> eventIds = [.. records.Select(r => r.AuditEventId)];
        Dictionary<Guid, AuditEvent> events = await context.Set<AuditEvent>().AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken)
            .ConfigureAwait(false);
        ILookup<Guid, AuditEventAttribute> attributes = (await context.Set<AuditEventAttribute>().AsNoTracking()
                .Where(a => eventIds.Contains(a.AuditEventId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToLookup(a => a.AuditEventId);

        return [.. records.Select(r => new PendingForwarding(r, events[r.AuditEventId], [.. attributes[r.AuditEventId]]))];
    }

    public Task SaveAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
