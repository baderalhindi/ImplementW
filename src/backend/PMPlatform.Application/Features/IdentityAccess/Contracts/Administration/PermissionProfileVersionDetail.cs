using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// A profile version and its grants; only a PUBLISHED version may be assigned (ADR-018). Who authored, validated and published it is part
/// of it: three different people (TASK-110).
/// </summary>
public sealed record PermissionProfileVersionDetail(
    Guid Id,
    int VersionNo,
    GovernedLifecycleState LifecycleState,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? RetiredAt,
    IReadOnlyList<PermissionProfileGrantDetail> Grants)
{
    public Guid PermissionProfileId { get; init; }

    public NarrativeText? ChangeSummary { get; init; }

    public Guid CreatedBy { get; init; }

    public Guid? ValidatedByUserId { get; init; }

    public Guid? PublishedByUserId { get; init; }
}
