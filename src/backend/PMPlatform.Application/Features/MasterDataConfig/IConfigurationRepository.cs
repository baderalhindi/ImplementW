using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// Configuration families, their versions and each version's typed rows. The database refuses any change to a version
/// that is not DRAFT beyond its own retirement, and any change to the rows of a version that is not DRAFT.
/// </summary>
public interface IConfigurationRepository
{
    public Task<IReadOnlyList<ConfigurationFamily>> ListFamiliesAsync(CancellationToken cancellationToken);

    public Task<ConfigurationFamily?> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken);

    public Task<ConfigurationFamily?> FindFamilyByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Every version of the families named that was ever published, retired ones included.</summary>
    public Task<IReadOnlyList<(Guid FamilyId, PublishedVersionWindow Window)>> ListPublishedWindowsAsync(
        IReadOnlyCollection<Guid> familyIds, CancellationToken cancellationToken);

    public Task<(IReadOnlyList<ConfigurationVersionRow> Rows, int TotalCount)> ListVersionsAsync(ConfigurationVersionQuery query, CancellationToken cancellationToken);

    public Task<Versioned<ConfigurationVersionRow>?> FindVersionAsync(Guid versionId, CancellationToken cancellationToken);

    public Task<ConfigurationVersion?> FindVersionForUpdateAsync(Guid versionId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<int> GetLatestVersionNoAsync(Guid familyId, CancellationToken cancellationToken);

    public Task<ConfigurationContent> ReadContentAsync(Guid versionId, CancellationToken cancellationToken);

    public void AddVersion(ConfigurationVersion version);

    /// <summary>
    /// Removes the version's typed rows and adds <paramref name="content"/> in their place, stamped with
    /// <paramref name="actorId"/> at <paramref name="now"/>; committed by <see cref="SaveAsync"/>.
    /// </summary>
    public Task StageContentAsync(Guid versionId, ConfigurationContent content, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
