using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.GlobalSearch.Contracts;

namespace PMPlatform.Application.Features.GlobalSearch;

/// <summary>The search across projects for the records that have only a per-project list: tasks, risks, concerns and change requests.</summary>
public interface IGlobalSearchRepository
{
    /// <summary>Records of <paramref name="type"/> whose title holds <paramref name="text"/>, within <paramref name="scope"/>, most recently changed first.</summary>
    public Task<IReadOnlyList<GlobalSearchHit>> SearchProjectRecordsAsync(
        GlobalSearchRecordType type, RecordScope scope, string text, int limit, CancellationToken cancellationToken);
}
