using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension.Contracts;

/// <summary>How a suspension or resumption request is named to WF-11 (ADR-003 §8.2 edges 23, 28) and routed in APPROVAL_AUTHORITY.</summary>
public static class SuspensionApprovalRouting
{
    public const string SubjectModule = "Suspension";

    public const string SubjectType = "SuspensionRequest";

    /// <summary>The route of a suspension request's review (TBC-SUS-002: AHDA configures the authority).</summary>
    public const string SuspensionRoutingKey = "SUSPENSION";

    /// <summary>The route of a resumption request's review, configured apart from a suspension's.</summary>
    public const string ResumptionRoutingKey = "RESUMPTION";

    public static string RoutingKeyOf(SuspensionRequestType type) => type switch
    {
        SuspensionRequestType.Suspend => SuspensionRoutingKey,
        SuspensionRequestType.Resume => ResumptionRoutingKey,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown request type."),
    };
}
