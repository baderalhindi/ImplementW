using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// WF-02's own definition of out of date (TASK-069 D-5): a period stays OPEN until its progress is published, so a project with an OPEN
/// period past its due date has not reported, and its latest published values are older than its reporting obligation. Nothing here
/// sets a threshold of its own (TBC-DSH-010).
/// </summary>
internal static class ReportingStaleness
{
    public static ILookup<Guid, ReportingCycleSummary> ByProject(IReadOnlyList<ReportingCycleSummary> cycles) => cycles.ToLookup(c => c.ProjectId);

    public static int OverduePeriods(IEnumerable<ReportingCycleSummary> cycles, DateOnly today) =>
        cycles.Count(c => c.Status == ReportingCycleStatus.Open && c.DueDate < today);
}
