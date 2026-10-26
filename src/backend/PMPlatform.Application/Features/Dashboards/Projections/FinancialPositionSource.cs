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

        Observation observation = (await ObserveAsync(request, cancellationToken).ConfigureAwait(false)).Single();
        return observation.Kind == ObservationKind.Masked ? ProjectionReading.Unknown(WidgetUnknownReason.Restricted) : ProjectionReadings.Of(observation);
    }

    /// <summary>One read of WF-14 per project: WF-14 decides each one's visibility and masking itself. A project it does not show is withheld.</summary>
    public async Task<IReadOnlyList<Observation>> ObserveAsync(ProjectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<Observation> observations = [];
        foreach (Guid projectId in request.ProjectIds)
        {
            FinancialPositionPage page = await financials.ListPositionsAsync(request.CallerId, projectId, One, cancellationToken).ConfigureAwait(false);
            observations.Add(page.Items.SingleOrDefault() is { } p
                ? FinancialReadings.Of(
                    projectId, p.ApprovedBudgetSar, p.ActualExpenditureToDateSar, p.ForecastAtCompletionSar, p.ValueStatus, p.FinancialStatus,
                    FinancialReadings.AsOf(p.AsOfDate) ?? p.ComputedAt, p.MaskedFields)
                : new Observation(projectId, ObservationKind.Masked, null, null));
        }

        return observations;
    }
}
