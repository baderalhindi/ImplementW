namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 13 (FinancialKpi → Progress, query): a project's reporting periods as WF-02 generated them, so WF-14's
/// periodic figures align to the same periods (TASK-052; progress-update.md F-13). It authorizes no one and generates nothing:
/// periods are WF-02's, created when progress is first reported for them.
/// </summary>
public interface IReportingCycleReader
{
    /// <summary>The project's periods, earliest first.</summary>
    public Task<IReadOnlyList<ReportingCycleSummary>> ListAsync(Guid projectId, CancellationToken cancellationToken);
}
