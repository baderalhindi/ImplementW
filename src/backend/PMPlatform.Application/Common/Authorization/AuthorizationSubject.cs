namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// The facts about one record the effective-authorization formula needs (Blueprint Section 10.1), supplied by the module
/// that owns the record as a value. The engine never reads a domain module's data (solution-architecture M-7).
/// </summary>
public sealed record AuthorizationSubject
{
    /// <summary>The project the record belongs to; a per-project grant (ADR-013) covers only this project.</summary>
    public Guid? ProjectId { get; init; }

    /// <summary>The owning AHDA department, for DEPT scope.</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>The delivering external entity, for ENTITY scope and cross-entity isolation.</summary>
    public Guid? ExternalEntityId { get; init; }

    /// <summary>The owner, for OWN scope.</summary>
    public Guid? OwnerUserId { get; init; }

    /// <summary>The people assigned to the record, for ASSIGNED scope.</summary>
    public IReadOnlyCollection<Guid> AssignedUserIds { get; init; } = [];

    /// <summary>False while the record's lifecycle state admits no change (e.g. submitted for approval, closed).</summary>
    public bool StateAllowsChange { get; init; } = true;

    /// <summary>
    /// The people who hold authority over the record's current workflow step. Null when no workflow step is being
    /// acted on; otherwise the caller must be one of them.
    /// </summary>
    public IReadOnlyCollection<Guid>? WorkflowActorUserIds { get; init; }

    /// <summary>The record's DATA_CLASSIFICATION item (ADR-010); null when unclassified.</summary>
    public Guid? DataClassificationItemId { get; init; }
}
