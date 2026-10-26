namespace PMPlatform.Application.Features.Approval.Contracts.Events;

/// <summary>How a run ended (event-conventions EV-11).</summary>
public enum ApprovalOutcomeDecision
{
    Approved = 1,
    Rejected = 2,
    Returned = 3,
    Withdrawn = 4,
}
