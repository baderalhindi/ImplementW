using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.GlobalSearch;
using PMPlatform.Application.Features.GlobalSearch.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;
using ProjectEntity = PMPlatform.Domain.Project.Project;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Infrastructure.Persistence.GlobalSearch;

/// <summary>
/// A read across the task, risk, concern and change request schemas, each row with its project's anchors. The anchors are the ones each module's
/// access check decides by (<c>SubjectOf</c>): the project, its department, its delivering entity, its Project Manager as owner, and, for a task or a
/// concern, its assignee. A risk or a change request has no assignee, so an ASSIGNED grant reaches neither, as the engine decides.
/// </summary>
internal sealed class GlobalSearchRepository(PMPlatformDbContext context) : IGlobalSearchRepository
{
    public async Task<IReadOnlyList<GlobalSearchHit>> SearchProjectRecordsAsync(
        GlobalSearchRecordType type, RecordScope scope, string text, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        string pattern = AdministrationPersistence.ContainsPattern(text);
        List<AnchoredRecord> rows = await RowsOf(type)
            .Where(PredicateComposition.AnyOf(scope.Clauses.Select(ClauseOf)))
            .Where(r => EF.Functions.ILike(r.Record.Title, pattern, AdministrationPersistence.LikeEscape))
            .OrderByDescending(r => r.Record.UpdatedAt).ThenByDescending(r => r.Record.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => new GlobalSearchHit(
            type, r.Record.Id, r.Record.ProjectId, new NarrativeText(r.Record.Title, r.Record.TitleLanguage), new NarrativeText(r.ProjectTitle, r.ProjectTitleLanguage), null))];
    }

    private IQueryable<AnchoredRecord> RowsOf(GlobalSearchRecordType type) =>
        from r in RecordsOf(type)
        join p in context.Set<ProjectEntity>().AsNoTracking() on r.ProjectId equals p.Id
        select new AnchoredRecord
        {
            Record = r,
            ProjectTitle = p.Title.Text,
            ProjectTitleLanguage = p.Title.Language,
            DepartmentId = p.DepartmentId,
            ExternalEntityId = p.ExternalEntityId,
            ProjectManagerUserId = p.ProjectManagerUserId,
        };

    private IQueryable<ProjectRecord> RecordsOf(GlobalSearchRecordType type) => type switch
    {
        GlobalSearchRecordType.ProjectTask => context.Set<ProjectTaskEntity>().AsNoTracking().Select(t => new ProjectRecord
        {
            Id = t.Id,
            ProjectId = t.ProjectId,
            Title = t.Title.Text,
            TitleLanguage = t.Title.Language,
            UpdatedAt = t.UpdatedAt,
            AssigneeUserId = t.AssigneeUserId,
        }),
        GlobalSearchRecordType.Risk => context.Set<RiskEntity>().AsNoTracking().Select(r => new ProjectRecord
        {
            Id = r.Id,
            ProjectId = r.ProjectId,
            Title = r.Title.Text,
            TitleLanguage = r.Title.Language,
            UpdatedAt = r.UpdatedAt,
            AssigneeUserId = null,
        }),
        GlobalSearchRecordType.ManagementConcern => context.Set<ConcernEntity>().AsNoTracking().Select(c => new ProjectRecord
        {
            Id = c.Id,
            ProjectId = c.ProjectId,
            Title = c.Title.Text,
            TitleLanguage = c.Title.Language,
            UpdatedAt = c.UpdatedAt,
            AssigneeUserId = c.AssigneeUserId,
        }),
        GlobalSearchRecordType.ChangeRequest => context.Set<ChangeRequestEntity>().AsNoTracking().Select(c => new ProjectRecord
        {
            Id = c.Id,
            ProjectId = c.ProjectId,
            Title = c.Title.Text,
            TitleLanguage = c.Title.Language,
            UpdatedAt = c.UpdatedAt,
            AssigneeUserId = null,
        }),
        GlobalSearchRecordType.Project or GlobalSearchRecordType.Document or _ =>
            throw new ArgumentOutOfRangeException(nameof(type), type, "Not a kind of record searched across projects."),
    };

    private static Expression<Func<AnchoredRecord, bool>> ClauseOf(RecordScopeClause clause)
    {
        Guid? project = clause.ProjectId;
        Guid? entity = clause.ExternalEntityId;
        Guid? department = clause.DepartmentId;
        Guid? owner = clause.OwnerUserId;
        Guid? assignee = clause.AssignedUserId;
        return r => (project == null || r.Record.ProjectId == project)
                    && (entity == null || r.ExternalEntityId == entity)
                    && (department == null || r.DepartmentId == department)
                    && (owner == null || r.ProjectManagerUserId == owner)
                    && (assignee == null || r.Record.AssigneeUserId == assignee);
    }

    private sealed class ProjectRecord
    {
        public required Guid Id { get; init; }

        public required Guid ProjectId { get; init; }

        public required string Title { get; init; }

        public required Language TitleLanguage { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }

        public required Guid? AssigneeUserId { get; init; }
    }

    private sealed class AnchoredRecord
    {
        public required ProjectRecord Record { get; init; }

        public required string ProjectTitle { get; init; }

        public required Language ProjectTitleLanguage { get; init; }

        public required Guid DepartmentId { get; init; }

        public required Guid? ExternalEntityId { get; init; }

        public required Guid? ProjectManagerUserId { get; init; }
    }
}
