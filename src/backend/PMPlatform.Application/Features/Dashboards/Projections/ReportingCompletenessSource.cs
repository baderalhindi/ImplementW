using PMPlatform.Application.Features.Progress.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// PROGRESS.REPORTING_COMPLETENESS (MET-RPT-COMP): whether an ACTIVE project's progress reporting is up to date, from WF-02's periods —
/// OVERDUE while one is past its due date unpublished. It measures the obligation, not the project's delay (US-DSH-EXE-012). A project
/// with no period generated yet is MISSING.
/// </summary>
internal sealed class ReportingCompletenessSource(IReportingCycleReader cycles) : IDashboardProjectionSource
{
    public const string UpToDate = "UP_TO_DATE";
    public const string Overdue = "OVERDUE";

    private static readonly IReadOnlyList<string> Order = [UpToDate, Overdue];

    public string ProjectionCode => DashboardProjections.ReportingCompleteness;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ILookup<Guid, ReportingCycleSummary> periods = ReportingStaleness.ByProject(await cycles.ListAsync(request.ProjectIds, cancellationToken).ConfigureAwait(false));
        List<Observation> observations = [.. request.ProjectIds.Select(id =>
        {
            if (!periods[id].Any())
            {
                return new Observation(id, ObservationKind.Missing, null, null);
            }

            int overdue = ReportingStaleness.OverduePeriods(periods[id], request.Today);
            return new Observation(id, ObservationKind.Current, request.Now,
                ProjectionReadings.State(overdue > 0 ? Overdue : UpToDate, ProjectionReadings.Count("OVERDUE_PERIODS", overdue)));
        })];
        return request.Read(observations, counted => ProjectionReadings.Distribution(counted, Order));
    }
}
