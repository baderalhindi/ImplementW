using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ChangeRequest;

/// <summary>
/// Permission to change one governed commitment, issued when a change request is approved (TASK-060). It is scoped — one project, one
/// purpose, one target — and version-pinned to the target's version the approvers reviewed; its target module applies it through a
/// typed adapter, exactly once, as the change takes effect there, and records its id on the row it creates. Issuing it changes no
/// target. Delete policy: RETAIN.
/// </summary>
public sealed class ChangeAuthorization : AuditedEntity
{
    public Guid ChangeRequestId { get; set; }

    /// <summary>The WF-11 run whose approval issued it.</summary>
    public Guid ApprovalInstanceId { get; set; }

    public ChangeAuthorizationScope AuthorizationScope { get; set; }

    /// <summary>The ADR-003 module that owns the target, e.g. Schedule.</summary>
    public required string TargetModule { get; set; }

    /// <summary>The target's aggregate, e.g. ProjectBaseline.</summary>
    public required string TargetType { get; set; }

    /// <summary>The target's identifier only (M-4).</summary>
    public Guid TargetId { get; set; }

    /// <summary>The target's version when approved; an application fails if the target has moved since.</summary>
    public int TargetRevisionNo { get; set; }

    /// <summary>Issuance's key, derived from the request, its revision and the scope: an approval issues each authorisation once.</summary>
    public required string IdempotencyKey { get; set; }

    public ChangeAuthorizationStatus Status { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    public Guid? AppliedByUserId { get; set; }

    /// <summary>
    /// The target module's record that applied it, as that module names it (e.g. <c>Schedule.ProjectBaseline:{id}</c>): the key of the
    /// application, so the same application retried is recognised and any other is refused.
    /// </summary>
    public string? AppliedReference { get; set; }
}
