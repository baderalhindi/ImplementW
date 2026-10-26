using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Closure.Contracts;

/// <summary>A completion or closure case's readiness history: every evaluation and waiver, newest first (CLO-CC-07, BR-CLO-031). CLOSEOUT_VIEW.</summary>
public interface IReadinessRecordService
{
    /// <summary>Empty for a case the caller may not see, or that does not exist (R-3).</summary>
    public Task<ReadinessRecordPage> ListAsync(Guid callerId, ReadinessRecordQuery query, PageRequest page, CancellationToken cancellationToken);
}
