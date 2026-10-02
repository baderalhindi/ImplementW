using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Project;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Project;

/// <summary>The <c>project</c> schema (TASK-041).</summary>
internal sealed class ProjectRepository(PMPlatformDbContext context) : IProjectRepository
{
    public async Task<ProjectEntity?> FindAsync(Guid projectId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        ProjectEntity? project = await context.Set<ProjectEntity>().SingleOrDefaultAsync(p => p.Id == projectId, cancellationToken).ConfigureAwait(false);
        if (project is not null)
        {
            AdministrationPersistence.ExpectVersion(context, project, expectedVersion);
        }

        return project;
    }

    public uint RowVersionOf(ProjectEntity project) =>
        context.Entry(project).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public async Task<(IReadOnlyList<ProjectEntity> Items, int TotalCount)> ListAsync(RecordScope scope, ProjectQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<ProjectEntity> rows = context.Set<ProjectEntity>().AsNoTracking().Where(Reached(scope));
        if (query.Statuses.Count > 0)
        {
            List<Domain.Project.ProjectLifecycleState> statuses = [.. query.Statuses];
            rows = rows.Where(p => statuses.Contains(p.LifecycleState));
        }

        if (query.DepartmentId is { } departmentId)
        {
            rows = rows.Where(p => p.DepartmentId == departmentId);
        }

        if (query.ExternalEntityId is { } externalEntityId)
        {
            rows = rows.Where(p => p.ExternalEntityId == externalEntityId);
        }

        if (query.ProjectManagerUserId is { } projectManagerUserId)
        {
            rows = rows.Where(p => p.ProjectManagerUserId == projectManagerUserId);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            string pattern = AdministrationPersistence.ContainsPattern(query.Q.Trim());
            rows = rows.Where(p => EF.Functions.ILike(p.Title.Text, pattern) || EF.Functions.ILike(p.FormalProjectId!, pattern));
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<ProjectEntity> items = await rows
            .OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.Id)
            .Skip(query.Page.Skip).Take(query.Page.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public Task<long> NextFormalProjectNumberAsync(CancellationToken cancellationToken) =>
        context.Database.SqlQuery<long>($"SELECT nextval('project.formal_project_id_seq') AS \"Value\"").SingleAsync(cancellationToken);

    public void Add(ProjectEntity project) => context.Add(project);

    public void Remove(ProjectEntity project) => context.Remove(project);

    public async Task<ProjectSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ProjectSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return ProjectSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The only unique key a project write can meet is its approval run's: another request started this revision's review.
            context.ChangeTracker.Clear();
            return ProjectSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            context.ChangeTracker.Clear();
            return ProjectSaveOutcome.InUse;
        }
    }

    /// <summary>
    /// <see cref="RecordScope"/> as SQL: the OR of its clauses, each the AND of its conditions on the project's own anchors. A
    /// project's owner is its Project Manager (indexing-strategy I-04). A project has no assignees, so an ASSIGNED clause
    /// reaches none; it carries no classification, so every clause clears it.
    /// </summary>
    private static Expression<Func<ProjectEntity, bool>> Reached(RecordScope scope) =>
        PredicateComposition.AnyOf(scope.Clauses.Where(c => c.AssignedUserId is null).Select(ClauseOf));

    private static Expression<Func<ProjectEntity, bool>> ClauseOf(RecordScopeClause clause)
    {
        Guid? project = clause.ProjectId;
        Guid? entity = clause.ExternalEntityId;
        Guid? department = clause.DepartmentId;
        Guid? owner = clause.OwnerUserId;
        return p => (project == null || p.Id == project)
                    && (entity == null || p.ExternalEntityId == entity)
                    && (department == null || p.DepartmentId == department)
                    && (owner == null || p.ProjectManagerUserId == owner);
    }
}
