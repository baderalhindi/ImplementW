namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>SCR-114: the delegations the caller gave and those given to them, newest first.</summary>
public sealed record ApprovalDelegationList(IReadOnlyList<ApprovalDelegationDetail> Given, IReadOnlyList<ApprovalDelegationDetail> Received);
