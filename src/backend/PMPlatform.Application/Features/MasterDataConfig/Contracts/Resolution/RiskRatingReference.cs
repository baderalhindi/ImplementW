using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

/// <summary>A rating of a published RISK_MATRIX version with the row id a record pins (ERD §7 row 12), e.g. HIGH.</summary>
public sealed record RiskRatingReference(Guid Id, string Code, BilingualLabel Label, short SortOrder);
