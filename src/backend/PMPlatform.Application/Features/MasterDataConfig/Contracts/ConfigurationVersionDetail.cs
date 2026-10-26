using PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary>
/// A configuration version: the unit of publication of one family, with its whole content. <see cref="Effectivity"/> is
/// derived now and is null for a version that was never published. The change summary and the content are its editable
/// representation while it is DRAFT.
/// </summary>
public sealed record ConfigurationVersionDetail(
    Guid Id,
    Guid FamilyId,
    string FamilyCode,
    int VersionNo,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    ConfigurationEffectivity? Effectivity,
    NarrativeText? ChangeSummary,
    ConfigurationContent Content,
    GovernedRecord Governance);
