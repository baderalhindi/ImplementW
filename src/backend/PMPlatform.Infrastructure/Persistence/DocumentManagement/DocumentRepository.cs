using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Domain.DocumentManagement;
using PMPlatform.Infrastructure.Persistence.Configurations.DocumentManagement;
using PMPlatform.Infrastructure.Persistence.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.DocumentManagement;

/// <summary>The <c>document_management</c> schema (TASK-037).</summary>
internal sealed class DocumentRepository(PMPlatformDbContext context) : IDocumentRepository
{
    /// <summary>
    /// Reads <c>project.project</c>: DocumentManagement is a foundation module and the Project module publishes no contract
    /// for its anchors yet (document-management.md F-6; the same read as identity-access-administration.md F-6).
    /// </summary>
    public Task<DocumentAnchors?> FindProjectAnchorsAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Set<ProjectEntity>().AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new DocumentAnchors(p.Id, p.DepartmentId, p.ExternalEntityId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Document?> FindDocumentAsync(Guid documentId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Document? document = await context.Set<Document>().SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken).ConfigureAwait(false);
        if (document is not null)
        {
            AdministrationPersistence.ExpectVersion(context, document, expectedVersion);
        }

        return document;
    }

    public uint RowVersionOf(Document document) =>
        context.Entry(document).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    public async Task<(IReadOnlyList<Document> Items, int TotalCount)> ListAsync(RecordScope scope, DocumentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<AnchoredDocument> rows =
            from d in context.Set<Document>().AsNoTracking()
            join p in context.Set<ProjectEntity>() on d.ProjectId equals p.Id into projects
            from p in projects.DefaultIfEmpty()
            select new AnchoredDocument { Document = d, DepartmentId = (Guid?)p.DepartmentId, ExternalEntityId = p.ExternalEntityId };

        rows = rows.Where(Reached(scope));
        if (query.ProjectId is { } projectId)
        {
            rows = rows.Where(r => r.Document.ProjectId == projectId);
        }

        if (query.Statuses.Count > 0)
        {
            List<DocumentStatus> statuses = [.. query.Statuses];
            rows = rows.Where(r => statuses.Contains(r.Document.Status));
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            string pattern = AdministrationPersistence.ContainsPattern(query.Q.Trim());
            rows = rows.Where(r => EF.Functions.ILike(r.Document.Title.Text, pattern));
        }

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        List<Document> items = await rows
            .OrderByDescending(r => r.Document.UpdatedAt).ThenByDescending(r => r.Document.Id)
            .Skip(query.Page.Skip).Take(query.Page.PageSize)
            .Select(r => r.Document)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return (items, total);
    }

    public async Task<IReadOnlyDictionary<Guid, DocumentVersion>> GetLatestVersionsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = [.. documentIds];
        List<DocumentVersion> versions = await context.Set<DocumentVersion>().AsNoTracking()
            .Where(v => ids.Contains(v.DocumentId)
                        && v.VersionNo == context.Set<DocumentVersion>().Where(o => o.DocumentId == v.DocumentId).Max(o => o.VersionNo))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return versions.ToDictionary(v => v.DocumentId);
    }

    public async Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(Guid documentId, CancellationToken cancellationToken) =>
        await context.Set<DocumentVersion>().AsNoTracking()
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.VersionNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, DocumentVersion>> GetVersionsByIdAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = [.. versionIds];
        return await context.Set<DocumentVersion>().AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<DocumentVersion?> FindVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        context.Set<DocumentVersion>().SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);

    public async Task<int> GetLatestVersionNoAsync(Guid documentId, CancellationToken cancellationToken) =>
        await context.Set<DocumentVersion>().Where(v => v.DocumentId == documentId).MaxAsync(v => (int?)v.VersionNo, cancellationToken).ConfigureAwait(false) ?? 0;

    public async Task<IReadOnlyList<Guid>> FindPendingScanIdsAsync(int count, CancellationToken cancellationToken) =>
        await context.Set<DocumentVersion>().AsNoTracking()
            .Where(v => v.ScanState == ScanState.ScanPending)
            .OrderBy(v => v.UpdatedAt).ThenBy(v => v.Id)
            .Select(v => v.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<BusinessLink>> GetLinksAsync(Guid documentId, CancellationToken cancellationToken) =>
        await context.Set<BusinessLink>().AsNoTracking()
            .Where(l => l.DocumentId == documentId)
            .OrderBy(l => l.LinkedAt).ThenBy(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<BusinessLink?> FindLinkAsync(Guid businessLinkId, CancellationToken cancellationToken) =>
        context.Set<BusinessLink>().SingleOrDefaultAsync(l => l.Id == businessLinkId, cancellationToken);

    public Task<BusinessLink?> FindLinkAsync(Guid documentId, BusinessTarget target, BusinessLinkRole role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        return context.Set<BusinessLink>().AsNoTracking()
            .SingleOrDefaultAsync(
                l => l.DocumentId == documentId && l.TargetModule == target.Module && l.TargetType == target.Type && l.TargetId == target.Id && l.LinkRole == role,
                cancellationToken);
    }

    public async Task<IReadOnlyList<BusinessLink>> GetActiveLinksToAsync(BusinessTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        return await LinksTo(target)
            .Where(l => l.UnlinkedAt == null)
            .OrderBy(l => l.LinkedAt).ThenBy(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EvidenceReference>> GetEvidenceAsync(IReadOnlyCollection<Guid> businessLinkIds, CancellationToken cancellationToken)
    {
        List<Guid> ids = [.. businessLinkIds];
        return await context.Set<EvidenceReference>()
            .Where(e => ids.Contains(e.BusinessLinkId))
            .OrderBy(e => e.DesignatedAt).ThenBy(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<EvidenceReference?> FindEvidenceAsync(Guid evidenceReferenceId, CancellationToken cancellationToken) =>
        context.Set<EvidenceReference>().SingleOrDefaultAsync(e => e.Id == evidenceReferenceId, cancellationToken);

    public Task<EvidenceReference?> FindEvidenceAsync(Guid businessLinkId, Guid documentVersionId, Guid evidenceTypeItemId, CancellationToken cancellationToken) =>
        context.Set<EvidenceReference>().AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.BusinessLinkId == businessLinkId && e.DocumentVersionId == documentVersionId && e.EvidenceTypeItemId == evidenceTypeItemId,
                cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetSatisfiedEvidenceTypesAsync(BusinessTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        List<Guid> types = await (
                from e in context.Set<EvidenceReference>().AsNoTracking()
                join l in LinksTo(target) on e.BusinessLinkId equals l.Id
                join v in context.Set<DocumentVersion>() on e.DocumentVersionId equals v.Id
                where e.Status == EvidenceReferenceStatus.Valid && l.UnlinkedAt == null && v.ScanState == ScanState.Clean
                select e.EvidenceTypeItemId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return types.ToHashSet();
    }

    public void Add(Document document) => context.Add(document);

    public void Add(DocumentVersion version) => context.Add(version);

    public void Add(BusinessLink link) => context.Add(link);

    public void Add(EvidenceReference evidence) => context.Add(evidence);

    public async Task<DocumentSaveOutcome> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return DocumentSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return DocumentSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
                                                  && KeyOutcome(violation.ConstraintName) is { } outcome)
        {
            context.ChangeTracker.Clear();
            return outcome;
        }
    }

    private static DocumentSaveOutcome? KeyOutcome(string? constraint) => constraint switch
    {
        DocumentVersionConfiguration.NumberKey => DocumentSaveOutcome.DuplicateVersion,
        BusinessLinkConfiguration.TargetKey => DocumentSaveOutcome.DuplicateLink,
        EvidenceReferenceConfiguration.PinKey => DocumentSaveOutcome.DuplicateEvidence,
        _ => null,
    };

    private IQueryable<BusinessLink> LinksTo(BusinessTarget target) =>
        context.Set<BusinessLink>().AsNoTracking()
            .Where(l => l.TargetModule == target.Module && l.TargetType == target.Type && l.TargetId == target.Id);

    /// <summary>
    /// <see cref="RecordScope"/> as SQL: the OR of its clauses, each the AND of its conditions on the document and its
    /// project's anchors. A document has no assignees, so an ASSIGNED clause reaches none. Every document is classified
    /// (the column is NOT NULL), so each clause requires a cleared classification.
    /// </summary>
    private static Expression<Func<AnchoredDocument, bool>> Reached(RecordScope scope)
    {
        ParameterExpression row = Expression.Parameter(typeof(AnchoredDocument), "r");
        Expression body = scope.Clauses
            .Where(c => c.AssignedUserId is null)
            .Select(c => (Expression)Expression.Invoke(ClauseOf(c), row))
            .Aggregate((Expression)Expression.Constant(false), Expression.OrElse);
        return Expression.Lambda<Func<AnchoredDocument, bool>>(new InvokeInliner().Visit(body), row);
    }

    private static Expression<Func<AnchoredDocument, bool>> ClauseOf(RecordScopeClause clause)
    {
        Guid? project = clause.ProjectId;
        Guid? entity = clause.ExternalEntityId;
        Guid? department = clause.DepartmentId;
        Guid? owner = clause.OwnerUserId;
        List<Guid> cleared = [.. clause.ClearedClassificationIds];
        return r => (project == null || r.Document.ProjectId == project)
                    && (entity == null || r.ExternalEntityId == entity)
                    && (department == null || r.DepartmentId == department)
                    && (owner == null || r.Document.OwnerUserId == owner)
                    && cleared.Contains(r.Document.DataClassificationItemId);
    }

    /// <summary>A document with its project's department and delivering entity; both null for a library document.</summary>
    private sealed class AnchoredDocument
    {
        public required Document Document { get; init; }

        public Guid? DepartmentId { get; init; }

        public Guid? ExternalEntityId { get; init; }
    }

    /// <summary>Replaces each <c>Invoke(lambda, arg)</c> with the lambda's body over the argument, so EF Core can translate it.</summary>
    private sealed class InvokeInliner : ExpressionVisitor
    {
        protected override Expression VisitInvocation(InvocationExpression node) =>
            node.Expression is LambdaExpression lambda
                ? Visit(new ParameterReplacer(lambda.Parameters[0], node.Arguments[0]).Visit(lambda.Body))
                : base.VisitInvocation(node);
    }

    private sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? replacement : base.VisitParameter(node);
    }
}
