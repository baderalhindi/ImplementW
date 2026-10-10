using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.ManagementConcern.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>MANAGEMENT_CONCERN.OPEN_CONCERNS: WF-07's issues and challenges neither RESOLVED nor CLOSED, through <see cref="IConcernCloseoutReader"/> (edge 50).</summary>
internal sealed class OpenConcernsSource(IConcernCloseoutReader concerns) : ProjectCountSource
{
    public override string ProjectionCode => DashboardProjections.OpenConcerns;

    protected override async Task<IReadOnlyList<WidgetFigure>> CountAsync(Guid projectId, CancellationToken cancellationToken) =>
        [ProjectionReadings.Count("OPEN_CONCERNS", await concerns.CountOpenAsync(projectId, cancellationToken).ConfigureAwait(false))];
}
