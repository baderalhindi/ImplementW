namespace PMPlatform.Application.Features.Suspension.Contracts;

/// <summary>The Suspension module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class SuspensionErrorCodes
{
    /// <summary>
    /// 422: a suspension request is raised, submitted, reviewed and effected while its project is ACTIVE; a resumption request while it is
    /// SUSPENDED under an open suspension (WF-09 BR-SUS-001, BR-SUS-002). A project in any other state takes neither.
    /// </summary>
    public const string ProjectNotEligible = "SUSPENSION_PROJECT_NOT_ELIGIBLE";

    /// <summary>
    /// 409: the project is already suspended, or another suspension request of it is not yet final — at most one active suspension and one
    /// open suspension request per project (BR-SUS-003, BR-SUS-004).
    /// </summary>
    public const string AlreadyExists = "SUSPENSION_ALREADY_EXISTS";

    /// <summary>409: another resumption request of the project's active suspension is not yet final (BR-SUS-005).</summary>
    public const string ResumptionAlreadyExists = "SUSPENSION_RESUMPTION_ALREADY_EXISTS";

    /// <summary>422: submitting a request without what review needs: its requested effective date.</summary>
    public const string Incomplete = "SUSPENSION_INCOMPLETE";

    /// <summary>
    /// 422: a requested effective date before today on submission, a planned resumption date not after the effective date, or a planned
    /// resumption date on a resumption request.
    /// </summary>
    public const string DateInvalid = "SUSPENSION_DATE_INVALID";

    /// <summary>409: the request is no longer a DRAFT or RETURNED: its fields are those it was submitted with. Only a DRAFT never submitted is deleted.</summary>
    public const string NotEditable = "SUSPENSION_NOT_EDITABLE";

    /// <summary>409: the request is under review; its originator withdraws the WF-11 run, which withdraws the request.</summary>
    public const string UnderReview = "SUSPENSION_UNDER_REVIEW";

    /// <summary>409: the approved request's effective date has not come; it is effected on or after that date.</summary>
    public const string NotYetEffective = "SUSPENSION_NOT_YET_EFFECTIVE";
}
