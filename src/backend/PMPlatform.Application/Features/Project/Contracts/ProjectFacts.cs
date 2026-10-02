using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// What another core module needs to know about a project for its own records (ADR-003 §8.2 edges 1–6): its identity,
/// the anchors its authorization is decided on, its lifecycle state, governance profile and the ADR-014 intake marker.
/// </summary>
public sealed record ProjectFacts(
    Guid Id,
    string? FormalProjectId,
    Guid DepartmentId,
    Guid? ExternalEntityId,
    Guid? ProjectManagerUserId,
    ProjectLifecycleState Status,
    Guid GovernanceProfileItemId,
    DateOnly? LegacyIntakeDate,
    DateTimeOffset? ActivatedAt);
