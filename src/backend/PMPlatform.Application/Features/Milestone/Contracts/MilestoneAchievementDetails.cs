using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Milestone;

namespace PMPlatform.Application.Features.Milestone.Contracts;

/// <summary>
/// One revision of a milestone's achievement claim. <see cref="IsCurrent"/> marks the milestone's ACCEPTED revision, whose
/// <see cref="AcceptedActualAchievementDate"/> is the milestone's Actual Achievement Date (ICD-04). A SUPERSEDED revision keeps
/// its accepted date and names the revision that superseded it. Its review history is
/// <c>GET /approval-instances?subjectModule=Milestone&amp;subjectType=MilestoneAchievement&amp;subjectId={id}</c>.
/// </summary>
public sealed record MilestoneAchievementDetail(
    Guid Id,
    Guid ProjectMilestoneId,
    Guid ProjectId,
    int RevisionNo,
    MilestoneAchievementStatus Status,
    bool IsCurrent,
    DateOnly ClaimedAchievementDate,
    DateOnly? AcceptedActualAchievementDate,
    NarrativeText? Narrative,
    Guid? SubmittedByUserId,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    NarrativeText? ReturnReason,
    Guid? SupersededByAchievementId,
    Guid? ProjectIntakeId,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>
/// A revision's evidence (PTBC-006): the EVIDENCE_POLICY version in force now and the evidence types it makes mandatory for the
/// milestone's category, the types the revision holds satisfying evidence for (a VALID reference to a CLEAN version on an
/// active link), and its links. <see cref="EvidencePolicyVersionId"/> is null while no EVIDENCE_POLICY version is published:
/// then nothing is mandatory yet, which the UI labels optional pending policy.
/// </summary>
public sealed record MilestoneEvidenceDetail(
    Guid MilestoneAchievementId,
    Guid? EvidencePolicyVersionId,
    IReadOnlyList<Guid> MandatoryEvidenceTypeItemIds,
    IReadOnlyList<Guid> SatisfiedEvidenceTypeItemIds,
    IReadOnlyList<BusinessLinkDetail> Links);
