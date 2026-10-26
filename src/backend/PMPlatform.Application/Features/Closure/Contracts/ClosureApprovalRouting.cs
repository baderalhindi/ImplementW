namespace PMPlatform.Application.Features.Closure.Contracts;

/// <summary>How a completion or closure case is named to WF-11 (ADR-003 §8.2 edges 27, 28) and routed in APPROVAL_AUTHORITY.</summary>
public static class ClosureApprovalRouting
{
    public const string SubjectModule = "Closure";

    public const string CompletionSubjectType = "CompletionCase";

    public const string ClosureSubjectType = "ClosureCase";

    /// <summary>The route of a completion case's review (TBC-CLO-001: AHDA configures the authority).</summary>
    public const string CompletionRoutingKey = "COMPLETION";

    /// <summary>The route of a closure case's review, terminal closures included (TBC-CLO-002, TBC-CLO-022).</summary>
    public const string ClosureRoutingKey = "CLOSURE";
}
