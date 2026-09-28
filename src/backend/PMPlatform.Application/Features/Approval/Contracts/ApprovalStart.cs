using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// A source module's request to approve one revision of a subject (ADR-003 §8.2 edges 20–27). The routing inputs —
/// governance profile, materiality band, amount — select the APPROVAL_AUTHORITY rows; the scope anchors are what the
/// approvers' grants must cover (M-7). The source has already authorised <see cref="RequestedByUserId"/> to submit.
/// </summary>
public sealed record ApprovalStart(
    ApprovalSubject Subject,
    string RoutingKey,
    Guid RequestedByUserId,
    Guid? ScopeProjectId,
    Guid? ScopeDepartmentId,
    Guid? GovernanceProfileItemId,
    short? BandNo,
    Money? AmountSar);
