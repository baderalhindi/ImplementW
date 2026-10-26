using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>A configuration family and the version resolution returns for it now, if any.</summary>
public sealed record ConfigurationFamilySummary(Guid Id, string Code, BilingualLabel Name, Guid? ActiveVersionId, int? ActiveVersionNo);
