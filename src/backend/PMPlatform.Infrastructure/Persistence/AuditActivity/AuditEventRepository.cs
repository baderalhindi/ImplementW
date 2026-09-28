using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.AuditActivity;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>
/// Writes <c>audit_activity</c> rows (TASK-033). <see cref="Stage"/> uses the request's context, so the producer's save
/// commits the event with its change; <see cref="AppendAsync"/> uses a context of its own, so a refusal is committed even
/// though nothing else in the request is, and nothing pending in the request is committed with it.
/// </summary>
internal sealed class AuditEventRepository(PMPlatformDbContext context, DbContextOptions<PMPlatformDbContext> options) : IAuditEventRepository
{
    public void Stage(AuditRecord record) => Add(context, record);

    public async Task AppendAsync(AuditRecord record)
    {
        await using PMPlatformDbContext own = new(options);
        Add(own, record);

        // Not cancellable: an aborted request is audited all the same (IAuditTrail.RecordAsync).
        await own.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static void Add(PMPlatformDbContext target, AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        target.Add(record.Event);
        target.AddRange(record.Attributes);
        if (record.Forwarding is { } forwarding)
        {
            target.Add(forwarding);
        }
    }
}
