using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// WF-01 registration (TASK-041): the Project master aggregate from DRAFT to APPROVED_PLANNED, and the one command that
/// makes it ACTIVE. Every lifecycle change is a command here or the WF-11 outcome of the review; nothing changes a
/// project's state implicitly — not a date reached, not progress reported. Each operation is decided by the authorization
/// engine on the project's anchors; a project the caller may not see is 404 (R-47).
/// </summary>
public interface IProjectService
{
    public Task<ProjectPage> ListAsync(Guid callerId, ProjectQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectDetail>>> GetAsync(Guid callerId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>A new DRAFT, revision 1, with no Formal Project ID. An entity user may create one for their own entity (ADR-013).</summary>
    public Task<AdministrationResult<Versioned<ProjectDetail>>> CreateAsync(Guid callerId, ProjectDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces the registration fields of a DRAFT or RETURNED project. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<ProjectDetail>>> UpdateAsync(
        Guid callerId, Guid projectId, ProjectDraft draft, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>HARD_DRAFT: a DRAFT is deleted by the person who created it; null when deleted.</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid projectId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>DRAFT → SUBMITTED, or RETURNED → SUBMITTED as the next revision, naming the Project Manager.</summary>
    public Task<AdministrationResult<Versioned<ProjectDetail>>> SubmitAsync(
        Guid callerId, Guid projectId, ProjectSubmission submission, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED → DRAFT, before AHDA has started its review.</summary>
    public Task<AdministrationResult<Versioned<ProjectDetail>>> WithdrawAsync(Guid callerId, Guid projectId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// SUBMITTED → UNDER_REVIEW, by AHDA, starting the revision's WF-11 run on the PROJECT_REGISTRATION route in the same
    /// transaction. Its outcome moves the project to APPROVED_PLANNED or RETURNED. An external user is refused (ADR-013).
    /// </summary>
    public Task<AdministrationResult<Versioned<ProjectDetail>>> StartReviewAsync(Guid callerId, Guid projectId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// APPROVED_PLANNED → ACTIVE, by AHDA: the only way a project becomes ACTIVE (Blueprint Sections 5, 9; ICD-02). An
    /// external user is refused (ADR-013).
    /// </summary>
    public Task<AdministrationResult<Versioned<ProjectDetail>>> ActivateAsync(Guid callerId, Guid projectId, uint? expectedVersion, CancellationToken cancellationToken);
}
