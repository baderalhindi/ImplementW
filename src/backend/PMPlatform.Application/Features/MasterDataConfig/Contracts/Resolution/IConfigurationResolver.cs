namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

/// <summary>
/// E-U2: every module resolves versioned policy configuration here, deterministically and on the backend, and fails
/// closed (Blueprint Section 12). Nothing is cached: a publication applies from its effective moment on.
/// </summary>
public interface IConfigurationResolver
{
    /// <summary>
    /// The version of <paramref name="familyCode"/> effective at <paramref name="asOf"/>: of the versions ever
    /// published, the one with the latest effective-from at or before it, unless that one was withdrawn by then. A past
    /// date returns the version effective then, not the current one.
    /// </summary>
    /// <exception cref="ConfigurationMissingException">No version, or more than one, is effective at that moment.</exception>
    public Task<ResolvedConfiguration> ResolveAsync(string familyCode, DateTimeOffset asOf, CancellationToken cancellationToken);

    /// <summary>A version a record pinned when it used it (ERD D-13): later publications never change what it resolves to.</summary>
    /// <exception cref="ConfigurationMissingException">The version does not exist or was never published.</exception>
    public Task<ResolvedConfiguration> ResolvePinnedAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>
    /// ADR-011: the ratings of a published RISK_MATRIX version with their row ids, which a record that pins the version stores
    /// beside it (ERD §7 row 12). The content of <see cref="ResolvedConfiguration"/> names a rating by code only.
    /// </summary>
    /// <exception cref="ConfigurationMissingException">The version does not exist or was never published.</exception>
    public Task<IReadOnlyList<RiskRatingReference>> ListRiskRatingsAsync(Guid versionId, CancellationToken cancellationToken);
}
