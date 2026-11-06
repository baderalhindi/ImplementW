namespace PMPlatform.Application.Features.GlobalSearch.Contracts;

public interface IGlobalSearchService
{
    /// <summary>
    /// The records matching <paramref name="text"/> that <paramref name="callerId"/> may read, each kind under its own view permission and data
    /// scope. Nothing is recorded: an empty result is not a refusal (api-conventions R-3).
    /// </summary>
    public Task<GlobalSearchResult> SearchAsync(Guid callerId, string text, CancellationToken cancellationToken);
}
