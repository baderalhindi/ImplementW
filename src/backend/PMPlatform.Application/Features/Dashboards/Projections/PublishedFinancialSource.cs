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
        if (request.Context == DashboardContextKind.Portfolio)
        {
            return FinancialReadings.Of(await financials.AggregateAsync(request.CallerId, request.ProjectIds, SemanticState.PublishedOfficial, cancellationToken).ConfigureAwait(false));
        }

        Guid projectId = request.ProjectIds.Single();
        PublishedFinancialSnapshotPage page = await financials.ListSnapshotsAsync(request.CallerId, projectId, Latest, cancellationToken).ConfigureAwait(false);
        return page.Items.SingleOrDefault() is { } s
            ? ProjectionReadings.Of(FinancialReadings.Of(
                projectId, s.ApprovedBudgetSar, s.ActualExpenditureToDateSar, s.ForecastAtCompletionSar, s.ValueStatus, s.FinancialStatus,
                FinancialReadings.AsOf(s.AsOfDate), s.MaskedFields))
            : ProjectionReading.Unknown(WidgetUnknownReason.Missing);
    }
}
