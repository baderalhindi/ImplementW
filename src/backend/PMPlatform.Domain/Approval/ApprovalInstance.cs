using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Approval;

/// <summary>
/// One approval run of one revision of a subject (TASK-035, ERD D-15). Subject-agnostic (solution-architecture M-8):
/// the subject is a module, a type, an id and a revision, never a source-module type. A decided run is never changed
/// again; a RETURNED subject comes back as a new revision with a new instance that links to this one. Delete policy: RETAIN.
/// </summary>
public sealed class ApprovalInstance : AuditedEntity
{
    public required string SubjectModule { get; set; }

    public required string SubjectType { get; set; }

    public Guid SubjectId { get; set; }

    public int SubjectRevisionNo { get; set; }

    /// <summary>The APPROVAL_AUTHORITY <c>subject_type_code</c> the stages are chosen by.</summary>
    public required string RoutingKey { get; set; }

    /// <summary>The APPROVAL_AUTHORITY version in force when the run started, pinned (ERD D-13): later publications never re-route it.</summary>
    public Guid AuthorityConfigurationVersionId { get; set; }

    /// <summary>Authorization anchor supplied by the source (M-7).</summary>
    public Guid? ScopeProjectId { get; set; }

    /// <summary>Authorization anchor supplied by the source (M-7).</summary>
    public Guid? ScopeDepartmentId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public ApprovalInstanceStatus Status { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>The outcome event's idempotency key (event-conventions EV-4): the outcome is published and applied once.</summary>
    public required string OutcomeIdempotencyKey { get; set; }

    /// <summary>When the source module's handler applied the outcome.</summary>
    public DateTimeOffset? OutcomeDeliveredAt { get; set; }

    /// <summary>The run of the previous revision of the same subject.</summary>
    public Guid? PreviousInstanceId { get; set; }
}
