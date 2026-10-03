using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Milestone.Contracts;

/// <summary>
/// WF-05's achievement claims (TASK-050). A claim is a revision of a shared milestone (ICD-04): opened DRAFT, evidenced and
/// edited, submitted to WF-11, and ACCEPTED or RETURNED by WF-11's outcome. A claim after a return, and a correction of an
/// accepted achievement, are each a new revision; accepting one supersedes the previously accepted revision, which is kept
/// as it was. Each operation is decided by the authorization engine on the milestone's project; a collection the caller may
/// not see is empty, and a revision they may not see is 404 (R-47).
/// </summary>
public interface IMilestoneAchievementService
{
    /// <summary>The revisions of one milestone, or of every milestone of one project, newest first.</summary>
    public Task<MilestoneAchievementPage> ListAsync(Guid callerId, MilestoneAchievementQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<MilestoneAchievementDetail>>> GetAsync(Guid callerId, Guid achievementId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the milestone's next revision as a DRAFT: its first claim, a claim after a return, or a correction of its accepted
    /// achievement. One revision of a milestone is open — DRAFT or SUBMITTED — at a time.
    /// </summary>
    public Task<AdministrationResult<Versioned<MilestoneAchievementDetail>>> CreateAsync(Guid callerId, MilestoneAchievementDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces a DRAFT's claim, as a whole. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<MilestoneAchievementDetail>>> UpdateAsync(
        Guid callerId, Guid achievementId, MilestoneAchievementChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>HARD_DRAFT: deletes a DRAFT and ends its evidence links. One that is not there is not an error (R-40).</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid achievementId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// DRAFT → SUBMITTED to WF-11, once the evidence EVIDENCE_POLICY makes mandatory for the milestone's category is held. From
    /// here the revision changes only by WF-11's outcome.
    /// </summary>
    public Task<AdministrationResult<Versioned<MilestoneAchievementDetail>>> SubmitAsync(Guid callerId, Guid achievementId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The revision's evidence links, and the evidence types the policy makes mandatory against those it holds.</summary>
    public Task<AdministrationResult<MilestoneEvidenceDetail>> GetEvidenceAsync(Guid callerId, Guid achievementId, CancellationToken cancellationToken);

    /// <summary>Attaches a document to a DRAFT and pins one of its CLEAN versions as evidence of a type (ADR-003 §8.2 edge 16).</summary>
    public Task<AdministrationResult<EvidenceReferenceDetail>> AttachEvidenceAsync(Guid callerId, Guid achievementId, MilestoneEvidenceAttachment attachment, CancellationToken cancellationToken);

    /// <summary>Withdraws a piece of a DRAFT's evidence. The document, its versions and the link's history remain.</summary>
    public Task<AdministrationResult<EvidenceReferenceDetail>> WithdrawEvidenceAsync(Guid callerId, Guid achievementId, Guid evidenceReferenceId, CancellationToken cancellationToken);
}
