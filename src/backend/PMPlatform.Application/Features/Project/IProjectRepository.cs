using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>The <c>project</c> schema (TASK-041). Finds that return rows to change track them; the others do not.</summary>
public interface IProjectRepository
{
    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ProjectEntity?> FindAsync(Guid projectId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Not tracked: for reading a project another unit of work does not change.</summary>
    public Task<ProjectEntity?> ReadAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked project, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(ProjectEntity project);

    /// <summary>The projects <paramref name="scope"/> reaches that match the query, most recently changed first, one page, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ProjectEntity> Items, int TotalCount)> ListAsync(RecordScope scope, ProjectQuery query, CancellationToken cancellationToken);

    /// <summary>Every project <paramref name="scope"/> reaches, unpaged, in id order. Not tracked.</summary>
    public Task<IReadOnlyList<ProjectEntity>> ListReachedAsync(RecordScope scope, CancellationToken cancellationToken);

    /// <summary>The next number of <c>project.formal_project_id_seq</c>. A number drawn in a transaction that rolls back is not reused.</summary>
    public Task<long> NextFormalProjectNumberAsync(CancellationToken cancellationToken);

    public void Add(ProjectEntity project);

    /// <summary>ADR-014: the intake declaration a project is taken in with, saved with it.</summary>
    public void Add(ProjectIntake intake);

    /// <summary>The project with this Formal Project ID, if any.</summary>
    public Task<Guid?> FindIdByFormalProjectIdAsync(string formalProjectId, CancellationToken cancellationToken);

    /// <summary>The projects whose legacy reference is one of <paramref name="legacyReferences"/>, by reference. Not tracked.</summary>
    public Task<IReadOnlyDictionary<string, ProjectEntity>> ListByLegacyReferencesAsync(IReadOnlyCollection<string> legacyReferences, CancellationToken cancellationToken);

    /// <summary>The projects whose Formal Project ID is one of <paramref name="formalProjectIds"/>, by that ID. Not tracked.</summary>
    public Task<IReadOnlyDictionary<string, ProjectEntity>> ListByFormalProjectIdsAsync(IReadOnlyCollection<string> formalProjectIds, CancellationToken cancellationToken);

    /// <summary>Tracked: the projects with these ids, for an import batch to update.</summary>
    public Task<IReadOnlyList<ProjectEntity>> FindManyAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>Tracked: the projects an import batch created (R3-4).</summary>
    public Task<IReadOnlyList<ProjectEntity>> ListCreatedByImportAsync(Guid projectImportBatchId, CancellationToken cancellationToken);

    /// <summary>R3-3: a dated follow-up note, saved with the project's change.</summary>
    public void Add(ProjectFollowUpNote note);

    /// <summary>A project's follow-up notes, newest first, one page, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ProjectFollowUpNote> Items, int TotalCount)> ListNotesAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Stages the removal of these projects' follow-up notes, with the projects a rolled-back import batch removes (R3-4).</summary>
    public Task RemoveFollowUpNotesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    public void Remove(ProjectEntity project);

    /// <summary>
    /// Saves the tracked changes, and whatever else this unit of work staged (its audit events, an approval run). A row changed
    /// since it was read and a deleted draft still referenced are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<ProjectSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}
