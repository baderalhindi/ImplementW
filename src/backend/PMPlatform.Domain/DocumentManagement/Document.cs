using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.DocumentManagement;

/// <summary>
/// A logical document (Blueprint Section 13): its title, type, classification and project. The files are its versions;
/// the row is never deleted, and its project never changes, because the project anchors who may reach it (ADR-013).
/// Delete policy: RETAIN.
/// </summary>
public sealed class Document : AuditedEntity
{
    public required NarrativeText Title { get; set; }

    public NarrativeText? Description { get; set; }

    /// <summary>Master data item of catalogue DOCUMENT_TYPE.</summary>
    public Guid DocumentTypeItemId { get; set; }

    /// <summary>Master data item of catalogue DATA_CLASSIFICATION: the sensitivity every reader must be cleared for (ADR-010).</summary>
    public Guid DataClassificationItemId { get; set; }

    /// <summary>Null for the general library (SCR-120).</summary>
    public Guid? ProjectId { get; set; }

    public Guid OwnerUserId { get; set; }

    public DocumentStatus Status { get; set; }
}
