using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Milestone.Contracts;

/// <summary>Exactly one of the two: the revisions of one milestone, or those of every milestone of one project.</summary>
public sealed record MilestoneAchievementQuery(Guid? ProjectMilestoneId, Guid? ProjectId);

/// <summary>A new revision of a milestone's achievement claim: the date it was achieved on, and what the claimant says of it.</summary>
public sealed record MilestoneAchievementDraft(Guid ProjectMilestoneId, DateOnly ClaimedAchievementDate, NarrativeText? Narrative);

/// <summary>A DRAFT's claim, as a whole (R-5).</summary>
public sealed record MilestoneAchievementChanges(DateOnly ClaimedAchievementDate, NarrativeText? Narrative);

/// <summary>A document to attach, the version of it to pin, and the EVIDENCE_TYPE item it is evidence of.</summary>
public sealed record MilestoneEvidenceAttachment(Guid DocumentId, Guid DocumentVersionId, Guid EvidenceTypeItemId);
