using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// E-U2 (TASK-034): the backend-authoritative resolution every module uses. It reads the database on each call, picks
/// by date alone (<see cref="EffectiveVersionSelection"/>), and throws rather than guess or default.
/// </summary>
internal sealed partial class ConfigurationResolver(IConfigurationRepository configuration, ILogger<ConfigurationResolver> logger) : IConfigurationResolver
{
    public async Task<ResolvedConfiguration> ResolveAsync(string familyCode, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(familyCode);

        ConfigurationFamily family = await configuration.FindFamilyByCodeAsync(familyCode, cancellationToken).ConfigureAwait(false)
                                     ?? throw FailClosed(familyCode, ConfigurationMissingReason.UnknownCode);
        List<PublishedVersionWindow> windows =
            [.. (await configuration.ListPublishedWindowsAsync([family.Id], cancellationToken).ConfigureAwait(false)).Select(w => w.Window)];
        EffectiveVersionChoice choice = EffectiveVersionSelection.Select(windows, asOf);
        if (choice.IsAmbiguous)
        {
            throw FailClosed(familyCode, ConfigurationMissingReason.AmbiguousVersions);
        }

        PublishedVersionWindow window = choice.Window ?? throw FailClosed(familyCode, ConfigurationMissingReason.NoEffectiveVersion);
        return await ContentOfAsync(family.Code, window, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResolvedConfiguration> ResolvePinnedAsync(Guid versionId, CancellationToken cancellationToken)
    {
        ConfigurationVersionRow? row = (await configuration.FindVersionAsync(versionId, cancellationToken).ConfigureAwait(false))?.Value;
        return row?.Version is not { PublishedAt: not null, EffectiveFrom: { } from } version
            ? throw FailClosed(row?.FamilyCode ?? versionId.ToString(), ConfigurationMissingReason.VersionNotPublished)
            : await ContentOfAsync(row.FamilyCode, new PublishedVersionWindow(version.Id, version.VersionNo, from, version.EffectiveTo), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RiskRatingReference>> ListRiskRatingsAsync(Guid versionId, CancellationToken cancellationToken)
    {
        ConfigurationVersionRow? row = (await configuration.FindVersionAsync(versionId, cancellationToken).ConfigureAwait(false))?.Value;
        return row?.Version is not { PublishedAt: not null }
            ? throw FailClosed(row?.FamilyCode ?? versionId.ToString(), ConfigurationMissingReason.VersionNotPublished)
            : [.. (await configuration.ListRiskRatingsAsync(versionId, cancellationToken).ConfigureAwait(false))
                .Select(r => new RiskRatingReference(r.Id, r.Code, r.Label, r.SortOrder))];
    }

    private async Task<ResolvedConfiguration> ContentOfAsync(string familyCode, PublishedVersionWindow window, CancellationToken cancellationToken)
    {
        ConfigurationContent content = await configuration.ReadContentAsync(window.VersionId, cancellationToken).ConfigureAwait(false);
        return new ResolvedConfiguration(window.VersionId, familyCode, window.VersionNo, window.EffectiveFrom, window.EffectiveTo, ContentOrder.Canonical(content));
    }

    private ConfigurationMissingException FailClosed(string code, ConfigurationMissingReason reason)
    {
        LogFailedClosed(logger, code, reason);
        return new ConfigurationMissingException(code, reason, entry: null);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Configuration resolution failed closed: {ConfigurationCode} {Reason}.")]
    private static partial void LogFailedClosed(ILogger logger, string configurationCode, ConfigurationMissingReason reason);
}
