using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// FINANCIAL_KPI.FINANCIAL_POSITION: WF-14's live financial position (CURRENT/LIVE) through <see cref="IFinancialProgressService"/>, which
/// authorises and masks for the caller again (ADR-010; defence in depth). Over a population, WF-14's own SAR-verified totals.
/// </summary>
internal sealed class FinancialPositionSource(IFinancialProgressService financials) : IDashboardProjectionSource
{
    private static readonly PageRequest One = new(1, 1);

    public string ProjectionCode => DashboardProjections.FinancialPosition;

    public async Task<ProjectionReading> ReadAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Context == DashboardContextKind.Portfolio)
        {
            return FinancialReadings.Of(await financials.AggregateAsync(request.CallerId, request.ProjectIds, SemanticState.CurrentLive, cancellationToken).ConfigureAwait(false));
        }

        Guid projectId = request.ProjectIds.Single();
        FinancialPositionPage page = await financials.ListPositionsAsync(request.CallerId, projectId, One, cancellationToken).ConfigureAwait(false);
        return page.Items.SingleOrDefault() is { } p
            ? ProjectionReadings.Of(FinancialReadings.Of(
                projectId, p.ApprovedBudgetSar, p.ActualExpenditureToDateSar, p.ForecastAtCompletionSar, p.ValueStatus, p.FinancialStatus,
                FinancialReadings.AsOf(p.AsOfDate) ?? p.ComputedAt, p.MaskedFields))
            : ProjectionReading.Unknown(WidgetUnknownReason.Restricted);
    }
}
