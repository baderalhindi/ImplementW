using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>A classified field of the FIELD_CLASSIFICATION configuration in force (ADR-010).</summary>
public sealed record FieldClassification(string FieldCode, Guid DataClassificationItemId, MaskingRule MaskingRule);
