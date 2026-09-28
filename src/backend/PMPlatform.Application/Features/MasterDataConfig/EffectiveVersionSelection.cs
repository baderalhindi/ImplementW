using PMPlatform.Application.Features.MasterDataConfig.Contracts;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// Deterministic as-of resolution over a family's published versions (TASK-034; ERD D-13). The chosen version is the
/// one with the latest effective-from at or before the moment, among versions not withdrawn before taking effect. If
/// that version was withdrawn by the moment, nothing is effective: an earlier version is never brought back.
/// </summary>
internal static class EffectiveVersionSelection
{
    public static EffectiveVersionChoice Select(IReadOnlyCollection<PublishedVersionWindow> windows, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(windows);

        List<PublishedVersionWindow> started = [.. windows.Where(w => !w.IsWithdrawnBeforeEffect && w.EffectiveFrom <= asOf)];
        if (started.Count == 0)
        {
            return EffectiveVersionChoice.None;
        }

        DateTimeOffset latestStart = started.Max(w => w.EffectiveFrom);
        List<PublishedVersionWindow> latest = [.. started.Where(w => w.EffectiveFrom == latestStart)];
        return latest switch
        {
            [_, _, ..] => EffectiveVersionChoice.Ambiguous,
            [{ EffectiveTo: { } to }] when to <= asOf => EffectiveVersionChoice.None,
            [var window] => EffectiveVersionChoice.Of(window),
            _ => EffectiveVersionChoice.None,
        };
    }

    /// <summary>Where <paramref name="window"/> stands at <paramref name="now"/> among its family's <paramref name="windows"/>.</summary>
    public static ConfigurationEffectivity EffectivityOf(PublishedVersionWindow window, IReadOnlyCollection<PublishedVersionWindow> windows, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(windows);

        return window.IsWithdrawnBeforeEffect || (window.EffectiveTo is { } to && to <= now)
            ? ConfigurationEffectivity.Retired
            : window.EffectiveFrom > now
            ? ConfigurationEffectivity.FutureEffective
            : windows.Any(w => !w.IsWithdrawnBeforeEffect && w.EffectiveFrom > window.EffectiveFrom && w.EffectiveFrom <= now)
            ? ConfigurationEffectivity.Superseded
            : ConfigurationEffectivity.Active;
    }
}
