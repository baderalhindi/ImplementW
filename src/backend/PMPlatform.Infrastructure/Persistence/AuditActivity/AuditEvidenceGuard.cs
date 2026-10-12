using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Infrastructure.Persistence.AuditActivity;

/// <summary>
/// Refuses, before anything is sent to the database, a save that would update or delete committed audit evidence
/// (<see cref="IAuditEvidence"/>; FG-06 AUD-CC-06, BR-AUD-003; TASK-073). Whatever code path tracks an event — an administrator's
/// request included — the save throws <see cref="AuditEvidenceImmutableException"/> and writes nothing. The database refuses the
/// same change for every role behind it (TASK-033 triggers), so a path that bypasses the context is refused there.
/// </summary>
internal sealed class AuditEvidenceGuard : ISaveChangesInterceptor
{
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Refuse(eventData);
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Refuse(eventData);
        return ValueTask.FromResult(result);
    }

    private static void Refuse(DbContextEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context?.ChangeTracker.Entries<IAuditEvidence>().FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted) is { } changed)
        {
            throw new AuditEvidenceImmutableException(changed.Entity.GetType().Name, changed.State == EntityState.Deleted ? "deleted" : "updated");
        }
    }
}

/// <summary>A save tried to update or delete committed audit evidence (FG-06 AUD-ERR-008 <c>AUDIT_EVENT_IMMUTABLE</c>).</summary>
public sealed class AuditEvidenceImmutableException : InvalidOperationException
{
    public const string Code = "AUDIT_EVENT_IMMUTABLE";

    public AuditEvidenceImmutableException()
    {
    }

    public AuditEvidenceImmutableException(string message)
        : base(message)
    {
    }

    public AuditEvidenceImmutableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal AuditEvidenceImmutableException(string entity, string change)
        : base($"{Code}: committed audit evidence ({entity}) cannot be {change}.")
    {
    }
}
