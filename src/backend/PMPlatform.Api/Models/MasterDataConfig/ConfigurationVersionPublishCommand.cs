namespace PMPlatform.Api.Models.MasterDataConfig;

/// <summary>When the version takes effect: now if absent, otherwise a later moment (FUTURE_EFFECTIVE until then).</summary>
public sealed record ConfigurationVersionPublishCommand(DateTimeOffset? EffectiveFrom);
