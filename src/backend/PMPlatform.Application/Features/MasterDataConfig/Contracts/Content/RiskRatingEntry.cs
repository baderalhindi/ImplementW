using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>A rating the matrix yields, e.g. HIGH. The code is unique within the version.</summary>
public sealed record RiskRatingEntry(string Code, BilingualLabel Label, short SortOrder);

/// <summary>A project stage in a PROJECT_STAGE version: active stages carry the weights, which total 100.</summary>
public sealed record ProjectStageEntry(string Code, BilingualLabel Name, short SortOrder, decimal WeightPercent, bool IsActive);
