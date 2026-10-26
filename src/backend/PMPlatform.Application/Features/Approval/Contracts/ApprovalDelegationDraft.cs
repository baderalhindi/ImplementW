namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>A delegation the caller gives. <see cref="ValidFrom"/> defaults to now; it may not lie in the past.</summary>
public sealed record ApprovalDelegationDraft(Guid DelegateUserId, string? RoutingKey, DateTimeOffset? ValidFrom, DateTimeOffset ValidTo);
