namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// One entry of the protected permission catalogue: its code, the group of the resource it acts on, and whether it
/// reads or changes. Holding any permission of a group that covers a record is what lets a caller see the record, so
/// a missing permission on a visible record is a 403 and an invisible record is a 404 (api-conventions R-47).
/// </summary>
public sealed record PermissionDefinition(string Code, string Group, AccessMode Mode);
