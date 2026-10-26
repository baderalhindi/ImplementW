using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Suspension.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// SUSPENSION.OPEN_REQUESTS: WF-09's suspension and resumption requests not yet REJECTED, WITHDRAWN or EFFECTED, through
/// <see cref="ISuspensionCloseoutReader"/> (edge 52). A request is never an active suspension: the lifecycle state says that.
/// </summary>
internal sealed class OpenSuspensionRequestsSource(ISuspensionCloseoutReader requests) : ProjectCountSource
{
    public override string ProjectionCode => DashboardProjections.OpenSuspensionRequests;

    protected override async Task<IReadOnlyList<WidgetFigure>> CountAsync(Guid projectId, CancellationToken cancellationToken) =>
        [ProjectionReadings.Count("OPEN_REQUESTS", await requests.CountOpenAsync(projectId, cancellationToken).ConfigureAwait(false))];
}
