using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>A row of SCR-101 My Requests, and the run an inbox item belongs to.</summary>
public sealed record ApprovalInstanceSummary(
    Guid Id,
    ApprovalSubject Subject,
    string RoutingKey,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAt,
    ApprovalInstanceStatus Status,
    DateTimeOffset? CompletedAt);
