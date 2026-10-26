using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// Versioned policy configuration (TASK-034; Blueprint Section 12). A version is authored as a DRAFT, its content
/// replaced as a whole while DRAFT, validated and published by two other people, and retired. A PUBLISHED version and
/// its content never change again; a change is a new version, copied from the old one if wanted.
/// </summary>
public interface IConfigurationAdministrationService
{
    public Task<IReadOnlyList<ConfigurationFamilySummary>> ListFamiliesAsync(CancellationToken cancellationToken);

    public Task<AdministrationResult<ConfigurationVersionPage>> ListVersionsAsync(ConfigurationVersionQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> CreateVersionAsync(Guid actorId, ConfigurationVersionDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces a DRAFT's change summary and whole content, by its author, at the version the caller last read (R-21).</summary>
    public Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> UpdateVersionAsync(
        Guid actorId, Guid versionId, ConfigurationVersionChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> ValidateVersionAsync(Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Takes effect at <paramref name="effectiveFrom"/>, or at once when null.</summary>
    public Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> PublishVersionAsync(
        Guid actorId, Guid versionId, DateTimeOffset? effectiveFrom, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// A DRAFT or VALIDATED version is abandoned. A published one is withdrawn from now on, or, if it has not taken
    /// effect yet, before it ever does; the dates of its past window stay, so past transactions still resolve to it.
    /// </summary>
    public Task<AdministrationResult<Versioned<ConfigurationVersionDetail>>> RetireVersionAsync(Guid actorId, Guid versionId, uint? expectedVersion, CancellationToken cancellationToken);
}
