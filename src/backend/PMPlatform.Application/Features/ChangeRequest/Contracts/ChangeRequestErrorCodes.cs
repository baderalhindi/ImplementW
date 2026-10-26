namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>The ChangeRequest module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class ChangeRequestErrorCodes
{
    /// <summary>
    /// 422: change requests are raised, edited, submitted and reviewed while the project is APPROVED_PLANNED, ACTIVE or SUSPENDED;
    /// implemented while it is APPROVED_PLANNED or ACTIVE; finished — marked implemented, closed — until it is CLOSED.
    /// </summary>
    public const string ProjectNotEligible = "CHANGE_REQUEST_PROJECT_NOT_ELIGIBLE";

    /// <summary>422: the request lacks what its change type needs to be submitted; every miss is a field issue.</summary>
    public const string Incomplete = "CHANGE_REQUEST_INCOMPLETE";

    /// <summary>422: the requested governance profile is not a PUBLISHED GOVERNANCE_PROFILE item, or is the project's own.</summary>
    public const string ProfileInvalid = "CHANGE_REQUEST_PROFILE_INVALID";

    /// <summary>
    /// 422: the commitment the request changes does not exist: a schedule impact needs an ACTIVE Approved Baseline, a cost impact an
    /// ACTIVE Approved Budget. Missing is never read as zero.
    /// </summary>
    public const string TargetUnavailable = "CHANGE_REQUEST_TARGET_UNAVAILABLE";

    /// <summary>409: the request is no longer a DRAFT or RETURNED: its fields are those it was submitted with. Only a DRAFT never submitted is deleted.</summary>
    public const string NotEditable = "CHANGE_REQUEST_NOT_EDITABLE";

    /// <summary>409: the request's review has started, so its materiality is recorded and read from it, not previewed.</summary>
    public const string Evaluated = "CHANGE_REQUEST_EVALUATED";

    /// <summary>409: the request is under review; its originator withdraws the WF-11 run, which withdraws the request.</summary>
    public const string UnderReview = "CHANGE_REQUEST_UNDER_REVIEW";

    /// <summary>409: an authorisation of the request has not been applied by its target module, so the change is not implemented.</summary>
    public const string AuthorizationPending = "CHANGE_REQUEST_AUTHORIZATION_PENDING";
}
