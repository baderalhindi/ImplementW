using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;

namespace PMPlatform.Application.Features.ExternalParticipation.Contracts;

/// <summary>A new request, DRAFT, for one project and one entity. The responder, reviewer and due date may follow before it is issued.</summary>
public sealed record ExternalUpdateRequestDraft(
    Guid ProjectId,
    Guid ExternalEntityId,
    Guid ContributionTypeItemId,
    Guid? TargetId,
    NarrativeText Instructions,
    Guid? ResponsibleUserId,
    Guid? ReviewerUserId,
    DateOnly? DueDate);

/// <summary>A DRAFT request's own fields, as a whole (R-5). Its project and entity are its scope and never change.</summary>
public sealed record ExternalUpdateRequestChanges(
    Guid ContributionTypeItemId,
    Guid? TargetId,
    NarrativeText Instructions,
    Guid? ResponsibleUserId,
    Guid? ReviewerUserId,
    DateOnly? DueDate);

/// <summary>The requests the caller may see, filtered by any of these that is set.</summary>
public sealed record ExternalUpdateRequestQuery(
    Guid? ProjectId, Guid? ExternalEntityId, IReadOnlyCollection<ExternalUpdateRequestStatus> Statuses, Guid? ResponsibleUserId, Guid? ReviewerUserId);

/// <summary>One answered field as the responder sends it: the value in its field's text form, and the language of a narrative.</summary>
public sealed record ContributionFieldInput(string FieldCode, string Value, Language? Language);

/// <summary>A review decision's words: the reason the entity reads, required to return or reject, and AHDA's internal note.</summary>
public sealed record ContributionReviewDecision(NarrativeText? Reason, NarrativeText? InternalNote);
