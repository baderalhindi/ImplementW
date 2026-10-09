using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ExternalParticipation.Contracts;

/// <summary>
/// WF-13 Path B (TASK-066): the entity's answer to a request, as immutable revisions. The request's responder drafts and submits; AHDA's
/// assigned reviewer accepts, returns or rejects the submitted revision as a whole. There is no command that changes a submitted value:
/// a correction is the next revision, which a return opens (WF-13 EXT-P-10, BR-EXT-011, BR-EXT-012).
/// </summary>
public interface IExternalContributionService
{
    /// <summary>The request's revisions, newest first; empty for a request the caller may not see.</summary>
    public Task<ExternalContributionPage> ListAsync(Guid callerId, Guid requestId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> GetAsync(Guid callerId, Guid contributionId, CancellationToken cancellationToken);

    /// <summary>Drafts revision 1 of the answer to an ISSUED request. EXTERNAL_CONTRIBUTION_RESPOND, the request's responder.</summary>
    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> StartAsync(
        Guid callerId, Guid requestId, IReadOnlyList<ContributionFieldInput> fields, CancellationToken cancellationToken);

    /// <summary>Replaces a DRAFT revision's values, as a whole. Requires the caller's version (R-21). The responder.</summary>
    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> UpdateAsync(
        Guid callerId, Guid contributionId, IReadOnlyList<ContributionFieldInput> fields, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>DRAFT → SUBMITTED, complete, pinning the source record's version and state; from here the revision never changes its values.</summary>
    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> SubmitAsync(Guid callerId, Guid contributionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED → UNDER_REVIEW. EXTERNAL_CONTRIBUTION_REVIEW, the request's reviewer, internal.</summary>
    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> StartReviewAsync(Guid callerId, Guid contributionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>UNDER_REVIEW → ACCEPTED_PENDING_APPLICATION, or APPLIED for a reference-only answer. Not an approval (BR-EXT-014). The reviewer.</summary>
    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> AcceptAsync(
        Guid callerId, Guid contributionId, ContributionReviewDecision decision, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>UNDER_REVIEW → RETURNED, with the entity's reason; opens the next revision as a DRAFT with the same values to correct. The reviewer.</summary>
    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> ReturnAsync(
        Guid callerId, Guid contributionId, ContributionReviewDecision decision, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>UNDER_REVIEW → REJECTED, with the entity's reason; the request closes. The reviewer.</summary>
    public Task<AdministrationResult<Versioned<ExternalContributionDetail>>> RejectAsync(
        Guid callerId, Guid contributionId, ContributionReviewDecision decision, uint? expectedVersion, CancellationToken cancellationToken);
}
