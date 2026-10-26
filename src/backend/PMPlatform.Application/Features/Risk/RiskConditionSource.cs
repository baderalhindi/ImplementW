using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// Revalidates a reminder about a risk before Notifications sends it (TASK-039, notification-runtime.md D-5): the risk's status
/// now, read from the database, never a cache. Risk publishes no reminder yet (risk-management.md F-5); the seam is in place for
/// the review reminders <c>next_review_date</c> drives.
/// </summary>
internal sealed class RiskConditionSource(IRiskRepository repository) : INotificationConditionSource
{
    public string SourceModule => RiskAudit.Module;

    public async Task<string?> FindStatusAsync(string subjectType, Guid subjectId, CancellationToken cancellationToken) =>
        subjectType == RiskAudit.RiskType && await repository.FindStatusAsync(subjectId, cancellationToken).ConfigureAwait(false) is { } status
            ? StatusText(status)
            : null;

    /// <summary>Upper snake case, as <c>risk.status</c> holds it.</summary>
    internal static string StatusText(RiskStatus status) => status switch
    {
        RiskStatus.Identified => "IDENTIFIED",
        RiskStatus.Assessed => "ASSESSED",
        RiskStatus.Treatment => "TREATMENT",
        RiskStatus.Monitoring => "MONITORING",
        RiskStatus.Closed => "CLOSED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown risk status."),
    };
}
