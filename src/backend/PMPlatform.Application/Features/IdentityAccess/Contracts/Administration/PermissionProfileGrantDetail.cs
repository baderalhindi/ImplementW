using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>One cell of the matrix: a catalogue permission at a data scope.</summary>
public sealed record PermissionProfileGrantDetail(Guid PermissionId, string PermissionCode, DataScope DataScope);
