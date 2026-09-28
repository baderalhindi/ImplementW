namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

public sealed record EvidenceRequirementEntry(Guid MilestoneCategoryItemId, Guid EvidenceTypeItemId, bool IsMandatory);
