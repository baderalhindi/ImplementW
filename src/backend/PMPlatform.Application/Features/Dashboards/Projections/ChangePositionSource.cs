using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.Dashboards.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// CHANGE_REQUEST.CHANGE_POSITION: WF-08's change requests undecided, approved and not started, and in implementation, through
/// <see cref="IChangeRequestCloseoutReader"/> (edge 51). The three are kept apart: approved is never implemented (FG-02 §12).
/// </summary>
internal sealed class ChangePositionSource(IChangeRequestCloseoutReader changes) : ProjectCountSource
{
    public override string ProjectionCode => DashboardProjections.ChangePosition;

    protected override async Task<IReadOnlyList<WidgetFigure>> CountAsync(Guid projectId, CancellationToken cancellationToken)
    {
        ChangeRequestCloseoutPosition position = await changes.ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
        return
        [
            ProjectionReadings.Count("UNDECIDED", position.Undecided),
            ProjectionReadings.Count("APPROVED_NOT_STARTED", position.ApprovedNotStarted),
            ProjectionReadings.Count("IN_IMPLEMENTATION", position.InImplementation),
        ];
    }
}
