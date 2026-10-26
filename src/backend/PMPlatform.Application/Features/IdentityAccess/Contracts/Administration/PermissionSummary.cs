using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>An entry of the protected permission catalogue. <see cref="DataClassificationItemId"/> is its clearance (ADR-010).</summary>
public sealed record PermissionSummary(Guid Id, string Code, BilingualLabel Name, string PermissionGroup, bool IsPrivileged, Guid? DataClassificationItemId);
