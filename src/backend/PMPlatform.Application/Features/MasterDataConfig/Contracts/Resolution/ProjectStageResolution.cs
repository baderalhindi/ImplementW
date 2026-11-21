using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

/// <summary>The active stages of a PROJECT_STAGE version, in stage order, with the version they come from.</summary>
public sealed record ProjectStageSet(Guid VersionId, int VersionNo, IReadOnlyList<ProjectStageEntry> Stages)
{
    public ProjectStageEntry? Find(string code) => Stages.FirstOrDefault(s => s.Code == code);
}

/// <summary>Review round 7, item 1: the project stages every module weighs progress by.</summary>
public static class ProjectStageResolution
{
    /// <summary>The PROJECT_STAGE version in force at <paramref name="asOf"/>, its active stages only; null when none is published or it has no active stage.</summary>
    public static async Task<ProjectStageSet?> ResolveStagesAsync(this IConfigurationResolver resolver, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        try
        {
            ProjectStageSet stages = Of(await resolver.ResolveAsync(ConfigurationFamilyCodes.ProjectStage, asOf, cancellationToken).ConfigureAwait(false));
            return stages.Stages.Count == 0 ? null : stages;
        }
        catch (ConfigurationMissingException)
        {
            return null;
        }
    }

    /// <summary>The version a stage-based period pinned, so its figures never move with a later version.</summary>
    public static async Task<ProjectStageSet> ResolvePinnedStagesAsync(this IConfigurationResolver resolver, Guid versionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        return Of(await resolver.ResolvePinnedAsync(versionId, cancellationToken).ConfigureAwait(false));
    }

    private static ProjectStageSet Of(ResolvedConfiguration version) =>
        new(version.VersionId, version.VersionNo, [.. version.Content.ProjectStages.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ThenBy(s => s.Code, StringComparer.Ordinal)]);
}
