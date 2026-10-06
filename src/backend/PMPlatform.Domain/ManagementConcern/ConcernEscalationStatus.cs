namespace PMPlatform.Domain.ManagementConcern;

/// <summary>ERD <c>concern_escalation.status</c>: an escalation is OPEN until it is RESOLVED by its addressee or WITHDRAWN by its escalator.</summary>
public enum ConcernEscalationStatus
{
    Open = 1,
    Resolved = 2,
    Withdrawn = 3,
}
