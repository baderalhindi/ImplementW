using PMPlatform.Domain.Common;
using PMPlatform.Domain.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>
/// An issue or a challenge. <see cref="SeverityItemId"/> is computed by the server from <see cref="Impacts"/> under the rule of
/// <see cref="SeverityConfigurationVersionId"/>, and is null until the concern is assessed; <see cref="PriorityItemId"/> is chosen by
/// people. <see cref="OpenEscalation"/> is the concern's OPEN escalation, if it has one: an overlay that never changes its status.
/// </summary>
public sealed record ConcernDetail(
    Guid Id,
    Guid ProjectId,
    ConcernType ConcernType,
    NarrativeText Title,
    NarrativeText Description,
    Guid CategoryItemId,
    Guid PriorityItemId,
    short? OverallImpactLevel,
    Guid? SeverityItemId,
    Guid? SeverityConfigurationVersionId,
    IReadOnlyList<ConcernImpactDetail> Impacts,
    ConcernStatus Status,
    int RevisionNo,
    Guid RaisedByUserId,
    DateTimeOffset RaisedAt,
    Guid? AssigneeUserId,
    Guid? OriginatingRiskId,
    DateOnly? TargetResolutionDate,
    DateOnly NextReviewDate,
    DateTimeOffset? LastReviewedAt,
    NarrativeText? Resolution,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    ConcernEscalationDetail? OpenEscalation,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record ConcernImpactDetail(Guid ImpactDimensionItemId, short ImpactLevel, NarrativeText? Rationale);

public sealed record ConcernEscalationDetail(
    Guid Id,
    Guid ManagementConcernId,
    int EscalationNo,
    Guid EscalatedByUserId,
    DateTimeOffset EscalatedAt,
    Guid EscalatedToRoleId,
    NarrativeText Reason,
    ConcernEscalationStatus Status,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedByUserId,
    NarrativeText? Resolution,
    DateTimeOffset UpdatedAt);

/// <summary>
/// An escalation as its command answers: <see cref="Replayed"/> when the request repeated an <c>Idempotency-Key</c> that had already
/// raised it, so nothing new was raised and nothing was published again (R-37).
/// </summary>
public sealed record EscalationOutcome(ConcernEscalationDetail Escalation, bool Replayed);
