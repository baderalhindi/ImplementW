using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Risk.Contracts;

/// <summary>
/// ADR-003 §8.2 edge 29, Dashboards → Risk (read projection; TASK-069): a project's open risks counted by the rating each one's
/// latest assessment recorded against its pinned RISK_MATRIX version. Never a rescoring (BR-DSH-004): an unassessed risk is
/// counted as such, not given a rating. It authorizes no one.
/// </summary>
public interface IRiskExposureReader
{
    /// <summary>One entry for each project named, in the order given; a project with no open risk has zero counts, which is the register's answer.</summary>
    public Task<IReadOnlyList<RiskExposure>> ListAsync(IReadOnlyList<Guid> projectIds, DateOnly today, CancellationToken cancellationToken);
}

/// <summary>A project's risks that are not CLOSED: by recorded rating, without an assessment yet, and past their next review date.</summary>
public sealed record RiskExposure(Guid ProjectId, int OpenCount, int NotAssessedCount, int ReviewOverdueCount, IReadOnlyList<RiskRatingCount> ByRating);

/// <summary>How many open risks carry one rating, as the rating's matrix version names and orders it.</summary>
public sealed record RiskRatingCount(string RatingCode, BilingualLabel Label, short SortOrder, int Count);
