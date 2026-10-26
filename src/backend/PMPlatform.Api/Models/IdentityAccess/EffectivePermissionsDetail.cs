using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// The permissions the caller holds now, each with the scopes it is held at, for the SPA to decide which menus, tabs and buttons to offer
/// (review C3). Read from the engine on every call, so a changed grant shows without a new sign-in. This is navigation, not protection:
/// every operation is decided again by the API.
/// </summary>
public sealed record EffectivePermissionsDetail(IReadOnlyList<EffectivePermissionDetail> Permissions);

/// <summary>One held permission and the scopes of the grants holding it, in their declared order.</summary>
public sealed record EffectivePermissionDetail(string Code, IReadOnlyList<DataScope> Scopes);
