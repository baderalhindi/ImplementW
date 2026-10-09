namespace PMPlatform.Domain.ExternalParticipation;

/// <summary>
/// ERD <c>external_update_request.status</c> (TASK-066). DRAFT is AHDA's and invisible to the entity; ISSUED awaits a first answer;
/// IN_PROGRESS has an answer being drafted, the first or the correction of a returned one; RESPONDED has the current revision with AHDA,
/// submitted, under review or accepted and awaiting application; CLOSED ended with that revision applied, rejected or found impossible
/// to apply; CANCELLED was withdrawn by AHDA before an answer reached it. CLOSED and CANCELLED are final. The ERD's EXPIRED is not
/// entered: lateness is the derived due condition (WF-13 §4.1), and no policy closes a request on its date.
/// </summary>
public enum ExternalUpdateRequestStatus
{
    Draft = 1,
    Issued = 2,
    InProgress = 3,
    Responded = 4,
    Closed = 5,
    Cancelled = 6,
}
