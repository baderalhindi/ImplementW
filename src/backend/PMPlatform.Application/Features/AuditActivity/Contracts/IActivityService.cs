using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.AuditActivity.Contracts;

/// <summary>SCR-057 and the source-record timeline (FG-06 §7): business Activity on the projects the caller may see now (BR-AUD-025).</summary>
public interface IActivityService
{
    public Task<ActivityEntryPage> ListAsync(Guid callerId, ActivityQuery query, PageRequest page, CancellationToken cancellationToken);
}
