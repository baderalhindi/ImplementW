using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>A scalar policy value, held as text and read as <see cref="Type"/>: e.g. a reminder offset or a freshness threshold.</summary>
public sealed record ConfigurationValueEntry(string Key, ConfigurationValueType Type, string Value);
