using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Project.Contracts;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>The <c>project</c> schema (TASK-041). Finds that return rows to change track them; the others do not.</summary>
public interface IProjectRepository
{
    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ProjectEntity?> FindAsync(Guid projectId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked project, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(ProjectEntity project);

    /// <summary>The projects <paramref name="scope"/> reaches that match the query, most recently changed first, one page, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ProjectEntity> Items, int TotalCount)> ListAsync(RecordScope scope, ProjectQuery query, CancellationToken cancellationToken);

    /// <summary>The next number of <c>project.formal_project_id_seq</c>. A number drawn in a transaction that rolls back is not reused.</summary>
    public Task<long> NextFormalProjectNumberAsync(CancellationToken cancellationToken);

    public void Add(ProjectEntity project);

    public void Remove(ProjectEntity project);

    /// <summary>
    /// Saves the tracked changes, and whatever else this unit of work staged (its audit events, an approval run). A row changed
    /// since it was read and a deleted draft still referenced are answers, not faults; after either nothing stays tracked.
    /// </summary>
    public Task<ProjectSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}
