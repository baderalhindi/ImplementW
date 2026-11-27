using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// A row of SCR-101 My Requests, and the run an inbox item belongs to. <see cref="Project"/> names the project the run is scoped to on a My
/// Requests row, the caller's own request (stage QA DEV-D1-03); null when the run has no project scope, and on every other listing.
/// </summary>
public sealed record ApprovalInstanceSummary(
    Guid Id,
    ApprovalSubject Subject,
    string RoutingKey,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAt,
    ApprovalInstanceStatus Status,
    DateTimeOffset? CompletedAt,
    ApprovalProjectName? Project = null);
