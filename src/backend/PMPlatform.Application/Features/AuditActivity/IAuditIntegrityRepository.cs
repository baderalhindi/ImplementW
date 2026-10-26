using PMPlatform.Application.Features.AuditActivity.Contracts;
using PMPlatform.Domain.AuditActivity;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>An event's link in the hash chain, with the hash the database computes from the row as it is now.</summary>
public sealed record AuditChainLink(Guid AuditEventId, DateTimeOffset RecordedAt, string EventHash, string? PreviousEventHash, string RecomputedHash, string? AttributeDigest);

/// <summary>The audit store as integrity verification reads it, and the pipeline's state as its status reports it (TASK-073).</summary>
public interface IAuditIntegrityRepository
{
    /// <summary>Up to <paramref name="count"/> links after <paramref name="after"/> (from the first when null), in chain order.</summary>
    public Task<IReadOnlyList<AuditChainLink>> ReadChainAsync(AuditStorePosition? after, int count, CancellationToken cancellationToken);

    public Task<IReadOnlyList<AuditEventAttribute>> ReadAttributesAsync(IReadOnlyCollection<Guid> auditEventIds, CancellationToken cancellationToken);

    public Task<AuditIngestionStatus> GetStatusAsync(CancellationToken cancellationToken);
}
