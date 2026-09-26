using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>A grant on the shipped-default profile of a canonical role, as db/seed writes it to that profile's version 1.</summary>
public sealed record ShippedGrant(string RoleCode, string PermissionCode, DataScope Scope);
