using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// WF-14's live financial status for WF-02's Overall Project Health. FinancialKpi already depends on Progress (edge 13), so the port is
/// Progress's and FinancialKpi implements it: Progress never references FinancialKpi, and the module graph keeps no cycle.
/// </summary>
public interface IProjectFinancialStatusSource
{
    /// <summary>The status as WF-14 rates it now; UNKNOWN when it cannot rate what was reported; null when nothing was reported.</summary>
    public Task<HealthStatus?> ReadAsync(Guid projectId, CancellationToken cancellationToken);
}
