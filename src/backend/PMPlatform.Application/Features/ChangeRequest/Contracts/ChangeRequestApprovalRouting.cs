namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>How a change request is named to WF-11 (ADR-003 §8.2 edge 22) and routed in APPROVAL_AUTHORITY.</summary>
public static class ChangeRequestApprovalRouting
{
    public const string SubjectModule = "ChangeRequest";

    public const string SubjectType = "ChangeRequest";

    /// <summary>The route of a change request's review, selected further by its materiality band and cost impact.</summary>
    public const string RoutingKey = "CHANGE_REQUEST";
}
