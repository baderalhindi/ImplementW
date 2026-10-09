using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ExternalParticipation.Contracts;

/// <summary>
/// WF-13's update requests (TASK-066): AHDA asks one entity for typed information on one project. Requests are drafted, issued and
/// cancelled by AHDA only; an external caller sees its own entity's issued requests, as the external projection, and nothing else. A request
/// the caller may not see is one that does not exist (R-47), and a collection holds only what the caller's data scope reaches.
/// </summary>
public interface IExternalUpdateRequestService
{
    /// <summary>The requests the caller's EXTERNAL_REQUEST_VIEW reaches, filtered in the query; an external caller's never include a DRAFT.</summary>
    public Task<ExternalUpdateRequestPage> ListAsync(Guid callerId, ExternalUpdateRequestQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> GetAsync(Guid callerId, Guid requestId, CancellationToken cancellationToken);

    /// <summary>Drafts a request. EXTERNAL_REQUEST_MANAGE, internal users.</summary>
    public Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> CreateAsync(Guid callerId, ExternalUpdateRequestDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces a DRAFT's own fields. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> UpdateAsync(
        Guid callerId, Guid requestId, ExternalUpdateRequestChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>HARD_DRAFT: only a DRAFT is deleted.</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid requestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>DRAFT → ISSUED, once everything WF-13 §5.3 asks of an issued request holds.</summary>
    public Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> IssueAsync(Guid callerId, Guid requestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>ISSUED or IN_PROGRESS → CANCELLED, with the reason; a draft answer stays as it was, and is never submitted.</summary>
    public Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> CancelAsync(
        Guid callerId, Guid requestId, NarrativeText reason, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Names another eligible responder of the entity on an issued request (WF-13 §9.4). Earlier revisions keep their contributor.</summary>
    public Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> AssignResponderAsync(
        Guid callerId, Guid requestId, Guid responsibleUserId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Names another eligible AHDA reviewer on an issued request.</summary>
    public Task<AdministrationResult<Versioned<ExternalUpdateRequestDetail>>> AssignReviewerAsync(
        Guid callerId, Guid requestId, Guid reviewerUserId, uint? expectedVersion, CancellationToken cancellationToken);
}
