using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Domain.FinancialKpi;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.FinancialKpi;

/// <inheritdoc cref="IProjectFinancialStatusSource"/>
internal sealed class ProjectFinancialStatusSource(IFinancialKpiRepository repository, FinancialPolicy policy, TimeProvider timeProvider) : IProjectFinancialStatusSource
{
    public async Task<HealthStatus?> ReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        Guid[] project = [projectId];
        if ((await repository.ListLatestReportedUpdatesAsync(project, cancellationToken).ConfigureAwait(false)).SingleOrDefault() is not { } update)
        {
            return null;
        }

        if (await policy.FindThresholdsAsync(timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false) is not { } rule)
        {
            return HealthStatus.Unknown;
        }

        FinancialCommitment? budget = (await repository.ListActiveBudgetsAsync(project, cancellationToken).ConfigureAwait(false)).SingleOrDefault();
        return FinancialStatusRule.Rate(budget?.AmountSar, update.ForecastAtCompletionSar, update.ValueStatus, rule.Thresholds) switch
        {
            FinancialStatus.Green => HealthStatus.Green,
            FinancialStatus.Amber => HealthStatus.Amber,
            FinancialStatus.Red => HealthStatus.Red,
            FinancialStatus.Unknown => HealthStatus.Unknown,
            _ => throw new InvalidOperationException("Unknown financial status."),
        };
    }
}
