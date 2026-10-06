namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>How a concern's resolution is named to WF-11 (ADR-003 §8.2 edge 24) and routed in APPROVAL_AUTHORITY.</summary>
public static class ConcernApprovalRouting
{
    public const string SubjectModule = "ManagementConcern";

    public const string SubjectType = "ManagementConcern";

    /// <summary>The route of a resolution's validation: PENDING_VALIDATION → RESOLVED, or back to IN_PROGRESS.</summary>
    public const string ResolutionRoutingKey = "MANAGEMENT_CONCERN_RESOLUTION";
}
