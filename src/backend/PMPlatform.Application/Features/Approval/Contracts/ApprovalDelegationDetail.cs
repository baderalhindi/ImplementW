using PMPlatform.Domain.Approval;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>A standing delegation (SCR-114). A null <see cref="RoutingKey"/> covers every routing key.</summary>
public sealed record ApprovalDelegationDetail(
    Guid Id,
    Guid DelegatorUserId,
    Guid DelegateUserId,
    string? RoutingKey,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    ApprovalDelegationStatus Status,
    DateTimeOffset? RevokedAt);
