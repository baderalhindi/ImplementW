using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ExternalParticipation;

/// <summary>
/// AHDA's request to one external entity for information on one project, for one typed purpose (WF-13 §5.1, TASK-066): the
/// contribution type names the typed schema the answer follows and the source record it may be applied to. It is prepared as a DRAFT,
/// which the entity never sees, issued to a named responder of the entity with a named AHDA reviewer, answered by
/// <see cref="ExternalContribution"/> revisions, and closed by the outcome of their review and application. Delete policy: HARD_DRAFT.
/// </summary>
public sealed class ExternalUpdateRequest : AuditedEntity
{
    public Guid ProjectId { get; set; }

    /// <summary>The entity the request is addressed to: the record's ENTITY scope anchor and its isolation boundary (ADR-013).</summary>
    public Guid ExternalEntityId { get; set; }

    public ExternalRequestOrigin Origin { get; set; }

    /// <summary>Master data item of catalogue CONTRIBUTION_TYPE; its code names the typed schema (WF-13 EXT-F-011).</summary>
    public Guid ContributionTypeItemId { get; set; }

    /// <summary>
    /// The typed schema the request is answered in, pinned as the contribution type's code when the request is drafted, so a later change
    /// of the catalogue never changes what an issued request asks for (WF-13 EXT-F-022, EXT-CC-29).
    /// </summary>
    public required string ContributionSchemaCode { get; set; }

    /// <summary>The PARTICIPATION version in force when the request was issued, which enabled its contribution type (WF-13 EXT-F-021).</summary>
    public Guid? ParticipationConfigurationVersionId { get; set; }

    /// <summary>The owning module of the source record an accepted answer is applied to; null for a reference-only purpose.</summary>
    public string? TargetModule { get; set; }

    public string? TargetType { get; set; }

    /// <summary>The source record, held as an identifier (M-4).</summary>
    public Guid? TargetId { get; set; }

    /// <summary>What AHDA asks for, stored as entered (ADR-012).</summary>
    public required NarrativeText Instructions { get; set; }

    /// <summary>The Primary External Responsible User: the one person of the entity who answers (WF-13 EXT-F-005).</summary>
    public Guid? ResponsibleUserId { get; set; }

    /// <summary>The AHDA user assigned to review the answer; never shown to the entity.</summary>
    public Guid? ReviewerUserId { get; set; }

    public DateOnly? DueDate { get; set; }

    public ExternalUpdateRequestStatus Status { get; set; }

    public Guid? IssuedByUserId { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public NarrativeText? CancellationReason { get; set; }

    /// <summary>When the request reached CLOSED: its answer applied, rejected, or found impossible to apply.</summary>
    public DateTimeOffset? ClosedAt { get; set; }
}
