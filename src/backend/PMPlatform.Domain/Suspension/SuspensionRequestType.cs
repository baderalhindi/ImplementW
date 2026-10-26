namespace PMPlatform.Domain.Suspension;

/// <summary>ERD <c>suspension_request.request_type</c> (TASK-062): what an approved request, once effected, does to its ACTIVE or SUSPENDED project.</summary>
public enum SuspensionRequestType
{
    /// <summary>ACTIVE → SUSPENDED, opening the project's one active suspension.</summary>
    Suspend = 1,

    /// <summary>SUSPENDED → ACTIVE, ending that suspension. The Approved Baseline is untouched: a rebaseline is WF-08's and WF-03's.</summary>
    Resume = 2,
}
