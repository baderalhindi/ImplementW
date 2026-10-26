using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.MasterDataConfig.Contracts.Content;

/// <summary>
/// One event family across ADR-004's three matrices: Mandatory or User-configurable (<see cref="IsMandatory"/>), its
/// channels (event family → channel) and the roles it reaches (recipient role → event family).
/// </summary>
public sealed record NotificationEventFamilyEntry(
    string Code,
    BilingualLabel Label,
    bool IsMandatory,
    IReadOnlyList<NotificationChannelEntry> Channels,
    IReadOnlyList<Guid> RecipientRoleIds);
