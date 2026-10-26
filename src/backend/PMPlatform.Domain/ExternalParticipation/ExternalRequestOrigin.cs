namespace PMPlatform.Domain.ExternalParticipation;

/// <summary>
/// ERD <c>external_update_request.origin</c>. Participation is request-driven (TASK-066 gate decision): every request is issued by AHDA.
/// The ERD's ENTITY_INITIATED, the specification's Conditional unsolicited intake (WF-13 §5.4), is not entered.
/// </summary>
public enum ExternalRequestOrigin
{
    AhdaIssued = 1,
}
