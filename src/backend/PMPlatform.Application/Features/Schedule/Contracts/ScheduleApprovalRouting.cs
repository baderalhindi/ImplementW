namespace PMPlatform.Application.Features.Schedule.Contracts;

/// <summary>
/// How a baseline names itself to WF-11 (M-8): the subject module and type, and the APPROVAL_AUTHORITY
/// <c>subject_type_code</c> AHDA configures the baseline route under.
/// </summary>
public static class ScheduleApprovalRouting
{
    public const string SubjectModule = "Schedule";

    public const string SubjectType = "ProjectBaseline";

    /// <summary>The route of a baseline candidate: SUBMITTED → ACTIVE, RETURNED, REJECTED or WITHDRAWN.</summary>
    public const string BaselineRoutingKey = "SCHEDULE_BASELINE";
}
