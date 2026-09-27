using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A profile version and its grants; only a PUBLISHED version may be assigned (ADR-018).</summary>
public sealed record PermissionProfileVersionDetail(
    Guid Id,
    int VersionNo,
    GovernedLifecycleState LifecycleState,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? RetiredAt,
    IReadOnlyList<PermissionProfileGrantDetail> Grants);
