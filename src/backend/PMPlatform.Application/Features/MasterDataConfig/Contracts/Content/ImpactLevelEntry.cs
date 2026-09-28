using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

public sealed record ImpactLevelEntry(
    Guid ImpactDimensionItemId, short Level, BilingualLabel Label, BilingualLabel? Description, decimal? LowerBound, decimal? UpperBound);
