namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// How a project names itself to WF-11 (M-8): the subject module and type, and the APPROVAL_AUTHORITY
/// <c>subject_type_code</c> AHDA configures the registration route under.
/// </summary>
public static class ProjectApprovalRouting
{
    public const string SubjectModule = "Project";

    public const string SubjectType = "Project";

    /// <summary>The route of a registration review: SUBMITTED → UNDER_REVIEW → RETURNED or APPROVED_PLANNED.</summary>
    public const string RegistrationRoutingKey = "PROJECT_REGISTRATION";
}
