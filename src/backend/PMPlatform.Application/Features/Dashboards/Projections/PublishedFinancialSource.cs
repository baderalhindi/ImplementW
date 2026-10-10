using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT: the latest Published Financial Snapshot (PUBLISHED/OFFICIAL) through
/// <see cref="IFinancialProgressService"/>, masked by audience there (ADR-010). Over a population, WF-14's own SAR-verified totals.
/// </summary>
internal sealed class PublishedFinancialSource(IFinancialProgressService financials) : IDashboardProjectionSource
{
    private static readonly PageRequest Latest = new(1, 1);

    public string ProjectionCode => DashboardProjections.PublishedFinancialSnapshot;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Context == DashboardContextKind.Portfolio
            ? FinancialReadings.Of(await financials.AggregateAsync(request.CallerId, request.ProjectIds, SemanticState.PublishedOfficial, cancellationToken).ConfigureAwait(false))
            : ProjectionReadings.Of((await ObserveAsync(request, cancellationToken).ConfigureAwait(false)).Single());
    }

    /// <summary>The latest snapshot of each project, one read of WF-14 per project; a project with none published is MISSING.</summary>
    public async Task<IReadOnlyList<Observation>> ObserveAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<Observation> observations = [];
        foreach (Guid projectId in request.ProjectIds)
        {
            PublishedFinancialSnapshotPage page = await financials.ListSnapshotsAsync(request.CallerId, projectId, Latest, cancellationToken).ConfigureAwait(false);
            observations.Add(page.Items.SingleOrDefault() is { } s
                ? FinancialReadings.Of(
                    projectId, s.ApprovedBudgetSar, s.ActualExpenditureToDateSar, s.ForecastAtCompletionSar, s.ValueStatus, s.FinancialStatus,
                    FinancialReadings.AsOf(s.AsOfDate), s.MaskedFields)
                : new Observation(projectId, ObservationKind.Missing, null, null));
        }

        return observations;
    }
}
