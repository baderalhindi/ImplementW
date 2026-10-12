namespace PMPlatform.Application.Features.AuditActivity.Contracts;

/// <summary>
/// Operating the audit store (AUD-API-035, -037, -039): its health, the integrity of its chain, and the Activity projection's
/// agreement with it. Each verification, check and rebuild is itself audited, by the person or service that ran it.
/// </summary>
public interface IAuditOperationsService
{
    public Task<AuditIngestionStatus> GetStatusAsync(CancellationToken cancellationToken);

    public Task<AuditIntegrityReport> VerifyIntegrityAsync(Guid actorUserId, CancellationToken cancellationToken);

    public Task<ActivityProjectionReport> CheckProjectionAsync(Guid actorUserId, CancellationToken cancellationToken);

    public Task<ActivityProjectionReport> RebuildProjectionAsync(Guid actorUserId, CancellationToken cancellationToken);
}
