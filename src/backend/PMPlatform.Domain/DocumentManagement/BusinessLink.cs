using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.DocumentManagement;

/// <summary>
/// A document attached to, or referenced by, a record of another module. The target is an identifier only (M-4).
/// Unlinking records when and by whom; it never deletes the link, the document or any version. Delete policy: RETAIN.
/// </summary>
public sealed class BusinessLink : AuditedEntity
{
    public Guid DocumentId { get; set; }

    public BusinessLinkRole LinkRole { get; set; }

    /// <summary>The ADR-003 name of the module that owns the target, e.g. <c>Milestone</c>.</summary>
    public required string TargetModule { get; set; }

    public required string TargetType { get; set; }

    public Guid TargetId { get; set; }

    public Guid LinkedByUserId { get; set; }

    public DateTimeOffset LinkedAt { get; set; }

    public DateTimeOffset? UnlinkedAt { get; set; }

    public Guid? UnlinkedByUserId { get; set; }

    public bool IsActive => UnlinkedAt is null;
}
