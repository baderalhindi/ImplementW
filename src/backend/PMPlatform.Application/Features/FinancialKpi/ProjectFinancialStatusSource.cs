using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <inheritdoc cref="IProjectFinancialStatusSource"/>
internal sealed class ProjectFinancialStatusSource(IFinancialKpiRepository repository, FinancialPolicy policy, TimeProvider timeProvider) : IProjectFinancialStatusSource
{
    public async Task<HealthStatus> ReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await policy.FindThresholdsAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false) is not { } rule)
        {
            return HealthStatus.Unknown;
        }

        Guid[] project = [projectId];
        FinancialCommitment? budget = (await repository.ListActiveBudgetsAsync(project, cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        FinancialProgressUpdate? update = (await repository.ListLatestReportedUpdatesAsync(project, cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        return FinancialStatusRule.Rate(budget?.AmountSar, update?.ForecastAtCompletionSar, update?.ValueStatus ?? ValueStatus.Missing, rule.Thresholds) switch
        {
            FinancialStatus.Green => HealthStatus.Green,
            FinancialStatus.Amber => HealthStatus.Amber,
            FinancialStatus.Red => HealthStatus.Red,
            FinancialStatus.Unknown => HealthStatus.Unknown,
            _ => throw new InvalidOperationException("Unknown financial status."),
        };
    }
}
