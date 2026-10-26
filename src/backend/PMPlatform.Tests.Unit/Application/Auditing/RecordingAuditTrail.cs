using PMPlatform.Application.Common.Auditing;

namespace PMPlatform.Tests.Unit.Application.Auditing;

/// <summary>An audit trail that keeps what producers hand it, so a test can assert on the events a path produces.</summary>
internal sealed class RecordingAuditTrail : IAuditTrail
{
    /// <summary>Entries added to the unit of work, to be committed with the producer's save.</summary>
    public List<AuditEntry> Staged { get; } = [];

    /// <summary>Entries committed on their own.</summary>
    public List<AuditEntry> Recorded { get; } = [];

    public IEnumerable<AuditEntry> All => Staged.Concat(Recorded);

    public void Stage(AuditEntry entry) => Staged.Add(entry);

    public Task RecordAsync(AuditEntry entry)
    {
        Recorded.Add(entry);
        return Task.CompletedTask;
    }
}
