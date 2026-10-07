using PMPlatform.Domain.Common;
using PMPlatform.Domain.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>
/// A new issue or challenge of the project, OPEN. Its impacts, if any, are on the dimension set risks and issues share (ADR-011);
/// its severity is computed from them by the server and is never part of a draft.
/// </summary>
public sealed record ConcernDraft(
    Guid ProjectId,
    ConcernType ConcernType,
    NarrativeText Title,
    NarrativeText Description,
    Guid CategoryItemId,
    Guid PriorityItemId,
    IReadOnlyList<ConcernImpactInput> Impacts,
    DateOnly? TargetResolutionDate)
{
    /// <summary>TASK-059 validation only: a severity the client claims, applied over the computed one.</summary>
    public Guid? ClaimedSeverityItemId { get; init; }
}

/// <summary>A concern's own fields, as a whole (R-5). Its type, project, status and severity change only through the commands.</summary>
public sealed record ConcernChanges(
    NarrativeText Title, NarrativeText Description, Guid CategoryItemId, Guid PriorityItemId, DateOnly? TargetResolutionDate);

public sealed record ConcernImpactInput(Guid ImpactDimensionItemId, short ImpactLevel, NarrativeText? Rationale);

/// <summary>A project's concerns, filtered by any of <see cref="ConcernTypes"/>, by any of <see cref="Statuses"/> and by assignee.</summary>
public sealed record ConcernQuery(Guid ProjectId, IReadOnlyCollection<ConcernType> ConcernTypes, IReadOnlyCollection<ConcernStatus> Statuses, Guid? AssigneeUserId);

/// <summary>An escalation of a concern, with the escalator's <c>Idempotency-Key</c>, which a retry repeats.</summary>
public sealed record ConcernEscalationDraft(Guid ManagementConcernId, NarrativeText Reason, Guid RequestKey);
