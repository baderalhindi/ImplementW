using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig;

/// <summary>A version as read, with its family's code.</summary>
public sealed record ConfigurationVersionRow(ConfigurationVersion Version, string FamilyCode);
