using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.AuditActivity.Contracts;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// The operations on the audit store (AUD-API-035, -037, -039). The caller is authorised by the API before it gets here
/// (<c>AUDIT_OPERATIONS_MANAGE</c>), or is the operator's command line acting as the audit-capture service principal. Each
/// verification, check and rebuild is audited after it ran, by whoever ran it, with its counts.
/// </summary>
internal sealed class AuditOperationsService(
    IAuditIntegrityRepository integrity, IActivityProjectionBuilder projection, IAuditTrail audit) : IAuditOperationsService
{
    public Task<AuditIngestionStatus> GetStatusAsync(CancellationToken cancellationToken) => integrity.GetStatusAsync(cancellationToken);

    public async Task<AuditIntegrityReport> VerifyIntegrityAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        AuditIntegrityReport report = await new AuditIntegrityVerifier(integrity).VerifyAsync(cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(AuditActivityAudit.IntegrityVerified(actorUserId, report)).ConfigureAwait(false);
        return report;
    }

    public async Task<ActivityProjectionReport> CheckProjectionAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        ActivityProjectionReport report = await projection.CheckAsync(cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(AuditActivityAudit.ProjectionChecked(actorUserId, report)).ConfigureAwait(false);
        return report;
    }

    public async Task<ActivityProjectionReport> RebuildProjectionAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        ActivityProjectionReport report = await projection.RebuildAsync(cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(AuditActivityAudit.ProjectionRebuilt(actorUserId, report)).ConfigureAwait(false);
        return report;
    }
}
