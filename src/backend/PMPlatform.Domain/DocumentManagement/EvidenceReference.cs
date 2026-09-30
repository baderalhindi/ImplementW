using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.DocumentManagement;

/// <summary>
/// A link designated as evidence of a type, pinned to one version of its document: a later version does not replace
/// it. Only a CLEAN version is referenced. Withdrawn, never deleted. Delete policy: RETAIN.
/// </summary>
public sealed class EvidenceReference : AuditedEntity
{
    public Guid BusinessLinkId { get; set; }

    /// <summary>The pinned version; a version of the link's document.</summary>
    public Guid DocumentVersionId { get; set; }

    /// <summary>Master data item of catalogue EVIDENCE_TYPE.</summary>
    public Guid EvidenceTypeItemId { get; set; }

    public Guid DesignatedByUserId { get; set; }

    public DateTimeOffset DesignatedAt { get; set; }

    public EvidenceReferenceStatus Status { get; set; }
}
