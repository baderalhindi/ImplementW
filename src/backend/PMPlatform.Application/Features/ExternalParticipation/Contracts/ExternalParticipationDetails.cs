using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation.Contracts;

/// <summary>
/// An update request as its caller may see it (WF-13 §8.2). AHDA's people get the <see cref="ParticipationAudience.Internal"/> view; an
/// external user always gets the <see cref="ParticipationAudience.External"/> projection, in which every internal-only field is null and
/// named in <see cref="MaskedFields"/>, and the API omits it (R-20): who reviews, who issued, who changed the row. The project is named by
/// its Formal Project ID, and the source record by its label.
/// </summary>
public sealed record ExternalUpdateRequestDetail(
    Guid Id,
    Guid ProjectId,
    string? FormalProjectId,
    Guid ExternalEntityId,
    ExternalRequestOrigin Origin,
    Guid ContributionTypeItemId,
    string ContributionSchemaCode,
    ContributionApplicationMode ApplicationMode,
    string? TargetModule,
    string? TargetType,
    Guid? TargetId,
    NarrativeText? TargetLabel,
    IReadOnlyList<ContributionFieldDefinition> ResponseFields,
    NarrativeText Instructions,
    Guid? ResponsibleUserId,
    Guid? ReviewerUserId,
    DateOnly? DueDate,
    ResponseDueCondition DueCondition,
    ExternalUpdateRequestStatus Status,
    Guid? ParticipationConfigurationVersionId,
    Guid? IssuedByUserId,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? CancelledAt,
    NarrativeText? CancellationReason,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy,
    ParticipationAudience Projection,
    IReadOnlyList<string> MaskedFields);

/// <summary>
/// One revision of an entity's answer, with its values. Its review reason is the entity's to read; the reviewer, the internal note and the
/// source version it was answered against are AHDA's, null and named in <see cref="MaskedFields"/> in the external projection.
/// </summary>
public sealed record ExternalContributionDetail(
    Guid Id,
    Guid ExternalUpdateRequestId,
    Guid ProjectId,
    Guid ExternalEntityId,
    int RevisionNo,
    Guid? PreviousRevisionId,
    ExternalContributionStatus Status,
    Guid ContributorUserId,
    IReadOnlyList<ContributionFieldValue> Fields,
    DateTimeOffset? SubmittedAt,
    long? TargetVersion,
    string? TargetState,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewStartedAt,
    DateTimeOffset? ReviewedAt,
    NarrativeText? ReviewReason,
    NarrativeText? ReviewInternalNote,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy,
    ParticipationAudience Projection,
    IReadOnlyList<string> MaskedFields);

/// <summary>A value of a revision, in its field's canonical text form; <see cref="Language"/> is set exactly for a narrative.</summary>
public sealed record ContributionFieldValue(string FieldCode, string Value, Language? Language);

/// <summary>One field of a contribution type's typed schema: what the entity may answer, never more (WF-13 BR-EXT-008).</summary>
public sealed record ContributionFieldDefinition(string FieldCode, ContributionFieldType FieldType, bool Required, decimal? Minimum, decimal? Maximum);

/// <summary>An attempt to apply an accepted revision to its source record (SCR-166). AHDA's only: an external caller never reads one.</summary>
public sealed record SourceApplicationDetail(
    Guid Id,
    Guid ExternalContributionId,
    Guid ExternalUpdateRequestId,
    int AttemptNo,
    SourceApplicationStatus Status,
    ContributionApplicationMode ApplicationMode,
    string? TargetModule,
    string? TargetType,
    Guid? TargetId,
    long? ExpectedTargetRevisionNo,
    long? ActualTargetRevisionNo,
    string? FailureCode,
    Guid AttemptedByUserId,
    DateTimeOffset AttemptedAt,
    DateTimeOffset CompletedAt,
    DateTimeOffset? RevalidatedAt,
    Guid? RevalidatedByUserId,
    long? RevalidatedTargetRevisionNo,
    Guid CorrelationId);

/// <summary>
/// An attempt as the apply command answers: <see cref="Replayed"/> when the request repeated an <c>Idempotency-Key</c> that had already made
/// it, so nothing was attempted again (R-37; WF-13 §20.4: "return the previously committed result").
/// </summary>
public sealed record SourceApplicationOutcome(SourceApplicationDetail Application, bool Replayed);

/// <summary>Which representation a caller gets (WF-13 §8.2): AHDA's full view, or the external least-disclosure projection.</summary>
public enum ParticipationAudience
{
    Internal = 1,
    External = 2,
}

/// <summary>
/// WF-13 EXT-F-020, derived on read and never stored: whether the entity's answer is due. NOT_APPLICABLE without a due date or once the
/// request awaits no answer from the entity. The specification's DUE_SOON waits on a policy threshold (TBC-EXT-014). It says nothing of the
/// project's, a task's or an approval's own timing (BR-EXT-035).
/// </summary>
public enum ResponseDueCondition
{
    NotApplicable = 1,
    NotDue = 2,
    Due = 3,
    Overdue = 4,
}

/// <summary>
/// WF-13 §7.1: what an accepted answer does to its source. REFERENCE_ONLY keeps it as information and changes no source; UPDATE_ALLOWED_SOURCE_FIELDS
/// sets narrowly allowlisted fields of one source record through the owning module's typed command, after revalidation.
/// </summary>
public enum ContributionApplicationMode
{
    ReferenceOnly = 1,
    UpdateAllowedSourceFields = 2,
}

/// <summary>WF-13 §5.2's item types the platform's schemas use: free text with its language, a number in a range, a date.</summary>
public enum ContributionFieldType
{
    Narrative = 1,
    Number = 2,
    Date = 3,
}
