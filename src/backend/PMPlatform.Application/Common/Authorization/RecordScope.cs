namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// The records a user reaches under one permission, as conditions a module can turn into a query: a record is reached if
/// any clause matches it. The engine builds it from the same grants and rules it decides a single record by, so for any
/// record <c>Matches(subject)</c> equals that decision's scope, relationship and classification terms (TASK-037;
/// <c>authorization-engine.md</c> F-8). Lifecycle state and workflow authority are not part of it: they concern changing
/// one record, not which records are listed.
/// </summary>
public sealed record RecordScope(IReadOnlyList<RecordScopeClause> Clauses)
{
    public static RecordScope None { get; } = new([]);

    public bool IsEmpty => Clauses.Count == 0;

    public bool Matches(AuthorizationSubject subject) => Clauses.Any(c => c.Matches(subject));
}

/// <summary>
/// One grant's reach. Every condition that is set must hold: each is an equality on one of the record's anchors
/// (<see cref="AuthorizationSubject"/>). A classified record must be of a classification in
/// <see cref="ClearedClassificationIds"/>; an unclassified one always passes.
/// </summary>
public sealed record RecordScopeClause
{
    /// <summary>ADR-013: a per-project assignment covers only this project.</summary>
    public Guid? ProjectId { get; init; }

    /// <summary>ENTITY scope, and ADR-013's cross-entity isolation of an external user.</summary>
    public Guid? ExternalEntityId { get; init; }

    public Guid? DepartmentId { get; init; }

    public Guid? OwnerUserId { get; init; }

    public Guid? AssignedUserId { get; init; }

    /// <summary>The classifications the grant's clearance covers (ADR-010); empty when it has none.</summary>
    public IReadOnlySet<Guid> ClearedClassificationIds { get; init; } = new HashSet<Guid>();

    public bool Matches(AuthorizationSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return (ProjectId is null || subject.ProjectId == ProjectId)
               && (ExternalEntityId is null || subject.ExternalEntityId == ExternalEntityId)
               && (DepartmentId is null || subject.DepartmentId == DepartmentId)
               && (OwnerUserId is null || subject.OwnerUserId == OwnerUserId)
               && (AssignedUserId is not { } assignee || subject.AssignedUserIds.Contains(assignee))
               && (subject.DataClassificationItemId is not { } classification || ClearedClassificationIds.Contains(classification));
    }
}
