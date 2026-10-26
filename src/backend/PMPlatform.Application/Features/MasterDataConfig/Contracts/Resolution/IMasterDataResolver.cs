namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

/// <summary>E-U2 for master data: the controlled values a module may use in a new record, failing closed on anything else.</summary>
public interface IMasterDataResolver
{
    /// <summary>The item, if it is a PUBLISHED item of <paramref name="catalogueCode"/>.</summary>
    /// <exception cref="ConfigurationMissingException">It does not exist, is of another catalogue, or is not PUBLISHED.</exception>
    public Task<MasterDataItemReference> RequirePublishedItemAsync(string catalogueCode, Guid itemId, CancellationToken cancellationToken);

    /// <summary>The PUBLISHED items of a catalogue in display order: what a form offers.</summary>
    /// <exception cref="ConfigurationMissingException">No catalogue has this code.</exception>
    public Task<IReadOnlyList<MasterDataItemReference>> ListPublishedItemsAsync(string catalogueCode, CancellationToken cancellationToken);
}
