using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>A rating the matrix yields, e.g. HIGH. The code is unique within the version.</summary>
public sealed record RiskRatingEntry(string Code, BilingualLabel Label, short SortOrder);
