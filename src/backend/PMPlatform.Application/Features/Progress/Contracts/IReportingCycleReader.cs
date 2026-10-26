namespace PMPlatform.Application.Features.Progress.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 13 (FinancialKpi → Progress, query): a project's reporting periods as WF-02 generated them, so WF-14's
/// periodic figures align to the same periods (TASK-052; progress-update.md F-13), and edge 29 (Dashboards → Progress) for
/// FG-01's reporting completeness (TASK-069). It authorizes no one and generates nothing:
/// periods are WF-02's, created when progress is first reported for them.
/// </summary>
public interface IReportingCycleReader
{
    /// <summary>The project's periods, earliest first.</summary>
    public Task<IReadOnlyList<ReportingCycleSummary>> ListAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The periods of the projects named, each project's earliest first: FG-01's reporting completeness and staleness (TASK-069).</summary>
    public Task<IReadOnlyList<ReportingCycleSummary>> ListAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);
}
