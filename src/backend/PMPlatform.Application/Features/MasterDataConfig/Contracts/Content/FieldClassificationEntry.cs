using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

public sealed record FieldClassificationEntry(string EntityCode, string FieldCode, Guid DataClassificationItemId, MaskingRule MaskingRule);
