namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>
/// A version that was published, by its dates. It is effective from <see cref="EffectiveFrom"/> until a later version
/// takes effect or <see cref="EffectiveTo"/> passes, whichever is first. A version withdrawn before it took effect has
/// <see cref="EffectiveTo"/> equal to <see cref="EffectiveFrom"/>: an empty window.
/// </summary>
public sealed record PublishedVersionWindow(Guid VersionId, int VersionNo, DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo)
{
    public bool IsWithdrawnBeforeEffect => EffectiveTo is { } to && to <= EffectiveFrom;
}
