namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>What as-of resolution found: one version, none, or more than one claiming the same moment.</summary>
internal sealed record EffectiveVersionChoice(PublishedVersionWindow? Window, bool IsAmbiguous)
{
    public static EffectiveVersionChoice None { get; } = new(null, false);

    public static EffectiveVersionChoice Ambiguous { get; } = new(null, true);

    public static EffectiveVersionChoice Of(PublishedVersionWindow window) => new(window, false);
}
