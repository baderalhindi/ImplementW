using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.Milestone.Contracts;

namespace PMPlatform.Application.Features.Dashboards.Projections;

/// <summary>
/// MILESTONE.OPEN_ACHIEVEMENT_CLAIMS: WF-05's achievement claims neither accepted nor superseded, through <see cref="IMilestoneCloseoutReader"/>
/// (edge 49). A claim is never an achievement: WF-05 decides that.
/// </summary>
internal sealed class OpenAchievementClaimsSource(IMilestoneCloseoutReader claims) : ProjectCountSource
{
    public override string ProjectionCode => DashboardProjections.OpenAchievementClaims;

    protected override async Task<IReadOnlyList<WidgetFigure>> CountAsync(Guid projectId, CancellationToken cancellationToken) =>
        [ProjectionReadings.Count("OPEN_ACHIEVEMENT_CLAIMS", await claims.CountOpenClaimsAsync(projectId, cancellationToken).ConfigureAwait(false))];
}
