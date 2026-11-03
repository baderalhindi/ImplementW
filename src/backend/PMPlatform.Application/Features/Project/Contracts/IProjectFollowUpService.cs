using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// RPMO's follow-up of registered projects (the 2 Nov 2026 review, R3-3, R3-5): the follow-up fields of one project, its dated notes, and the
/// Follow-up list of every project that needs following up. AHDA keeps it (PROJECT_FOLLOW_UP, internal users only); whoever may see a project
/// reads it.
/// </summary>
public interface IProjectFollowUpService
{
    /// <summary>
    /// Replaces the follow-up of a project from APPROVED_PLANNED on (R-5), with an optional note. Declared values changed here are
    /// recorded as MANUAL. Requires the caller's version (R-21).
    /// </summary>
    public Task<AdministrationResult<Versioned<ProjectDetail>>> UpdateAsync(
        Guid callerId, Guid projectId, ProjectFollowUpChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<ProjectFollowUpNotePage>> ListNotesAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>
    /// قائمة المتابعة: the projects the caller may see that have a follow-up reason, most urgent first — stalled, suspended, late, an overdue
    /// action, an open obstacle — then the earliest action due.
    /// </summary>
    public Task<ProjectFollowUpPage> ListAsync(Guid callerId, ProjectFollowUpQuery query, CancellationToken cancellationToken);
}

public sealed record ProjectFollowUpNotePage(IReadOnlyList<ProjectFollowUpNoteDetail> Items, int Page, int PageSize, int TotalCount);

public sealed record ProjectFollowUpPage(IReadOnlyList<ProjectFollowUpItem> Items, int Page, int PageSize, int TotalCount);

/// <summary>The Follow-up list's filters, each narrowing the caller's scope: a department, a reason, a follow-up owner.</summary>
public sealed record ProjectFollowUpQuery(Guid? DepartmentId, FollowUpReason? Reason, Guid? FollowUpOwnerUserId, PageRequest Page);
