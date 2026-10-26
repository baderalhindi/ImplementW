using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

public sealed record ProbabilityLevelEntry(short Level, BilingualLabel Label, decimal? LowerPct, decimal? UpperPct);
