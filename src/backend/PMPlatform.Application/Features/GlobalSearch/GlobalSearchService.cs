using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.GlobalSearch.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.GlobalSearch;

/// <summary>
/// The header search. Projects and documents come from their own lists, so they keep those lists' reach exactly. The kinds with only a per-project
/// list are searched under the same permission their list and detail check, with the same anchors (global-search.md D-2).
/// </summary>
internal sealed class GlobalSearchService(
    IProjectService projects, IDocumentService documents, IAuthorizationEngine engine, IGlobalSearchRepository repository) : IGlobalSearchService
{
    /// <summary>Below this a search would match almost everything.</summary>
    public const int MinimumTextLength = 2;

    private const int MatchesPerType = 5;

    private static readonly PageRequest FirstMatches = new(1, MatchesPerType);

    private static readonly (GlobalSearchRecordType Type, string Permission)[] ProjectRecords =
    [
        (GlobalSearchRecordType.ProjectTask, PermissionCatalogue.TaskView),
        (GlobalSearchRecordType.Risk, PermissionCatalogue.RiskView),
        (GlobalSearchRecordType.ManagementConcern, PermissionCatalogue.ConcernView),
        (GlobalSearchRecordType.ChangeRequest, PermissionCatalogue.ChangeRequestView),
    ];

    public async Task<GlobalSearchResult> SearchAsync(Guid callerId, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        string wanted = text.Trim();
        if (wanted.Length < MinimumTextLength)
        {
            return new GlobalSearchResult([]);
        }

        ProjectPage matchedProjects = await projects.ListAsync(callerId, new ProjectQuery([], null, null, null, wanted, FirstMatches), cancellationToken).ConfigureAwait(false);
        List<GlobalSearchHit> hits =
            [.. matchedProjects.Items.Select(p => new GlobalSearchHit(GlobalSearchRecordType.Project, p.Id, p.Id, p.Title, null, p.FormalProjectId))];

        foreach ((GlobalSearchRecordType type, string permission) in ProjectRecords)
        {
            RecordScope scope = await engine.GetRecordScopeAsync(callerId, permission, cancellationToken).ConfigureAwait(false);
            if (!scope.IsEmpty)
            {
                hits.AddRange(await repository.SearchProjectRecordsAsync(type, scope, wanted, MatchesPerType, cancellationToken).ConfigureAwait(false));
            }
        }

        DocumentPage matchedDocuments = await documents.ListAsync(callerId, new DocumentQuery(null, [], wanted, FirstMatches), cancellationToken).ConfigureAwait(false);
        hits.AddRange(matchedDocuments.Items.Select(d => new GlobalSearchHit(GlobalSearchRecordType.Document, d.Id, d.ProjectId, d.Title, null, null)));
        return new GlobalSearchResult(hits);
    }
}
