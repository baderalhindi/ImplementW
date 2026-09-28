using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts;

/// <summary><see cref="Effectivity"/> is null for a version that was never published.</summary>
public sealed record ConfigurationVersionSummary(
    Guid Id,
    Guid FamilyId,
    string FamilyCode,
    int VersionNo,
    GovernedLifecycleState LifecycleState,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    ConfigurationEffectivity? Effectivity);
