namespace PMPlatform.Application.Features.Approval.Contracts.Events;

/// <summary>
/// <c>ApprovalOutcomeData</c> (event-conventions EV-11), the same for every source. <see cref="DecidedByUserId"/> is the
/// person who acted last: the final approver, the rejecter or returner, or the requester who withdrew.
/// </summary>
public sealed record ApprovalOutcomeData(
    Guid ApprovalInstanceId,
    string RoutingKey,
    ApprovalOutcomeDecision Decision,
    DateTimeOffset DecidedAt,
    Guid DecidedByUserId,
    Guid AuthorityConfigurationVersionId);
