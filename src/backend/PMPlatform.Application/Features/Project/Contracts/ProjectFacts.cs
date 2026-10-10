using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// What another core module needs to know about a project for its own records (ADR-003 §8.2 edges 1–6): its identity,
/// the anchors its authorization is decided on, its lifecycle state, governance profile, the ADR-014 intake marker, and who manages its
/// record day to day (ADR-013), on which FG-04's participation rules are keyed (TASK-066). <see cref="Title"/> names the project as entered (ADR-012) for
/// FG-02's report rows (edge 30 through edge 47, TASK-071).
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
    DateTimeOffset? ActivatedAt,
    ParticipationMode ParticipationMode,
    NarrativeText Title);
