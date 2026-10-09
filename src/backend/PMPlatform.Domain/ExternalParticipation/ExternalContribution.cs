using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ExternalParticipation;

/// <summary>
/// One revision of the entity's answer to an <see cref="ExternalUpdateRequest"/> (WF-13 Path B, TASK-066). A revision is drafted by the
/// request's responder, and once submitted its values (<see cref="ExternalContributionField"/>) and the source version it was answered
/// against never change. AHDA's reviewer accepts, returns or rejects it as a whole; a return opens the next revision as a new DRAFT, so a
/// correction is always a new revision and never an edit (WF-13 EXT-P-10, BR-EXT-011). The project and entity are the request's,
/// copied so the revision carries its own scope anchors. Delete policy: HARD_DRAFT.
/// </summary>
public sealed class ExternalContribution : AuditedEntity
{
    public Guid ExternalUpdateRequestId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid ExternalEntityId { get; set; }

    /// <summary>The named person of the entity who drafted the revision and, once it is submitted, submitted it (WF-13 §23.3).</summary>
    public Guid ContributorUserId { get; set; }

    public int RevisionNo { get; set; } = 1;

    public ExternalContributionStatus Status { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>The source record's row version when the revision was submitted: what its application expects to find (WF-13 EXT-F-073).</summary>
    public long? TargetVersion { get; set; }

    /// <summary>The source record's state when the revision was submitted (WF-13 EXT-F-074).</summary>
    public string? TargetState { get; set; }

    /// <summary>The AHDA reviewer who started the review and decided it.</summary>
    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewStartedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Why it was returned or rejected, as the entity sees it (WF-13 EXT-F-127).</summary>
    public NarrativeText? ReviewReason { get; set; }

    /// <summary>The reviewer's internal note, never shown to the entity (WF-13 EXT-P-12).</summary>
    public NarrativeText? ReviewInternalNote { get; set; }

    /// <summary>The returned revision this one corrects.</summary>
    public Guid? PreviousRevisionId { get; set; }
}
