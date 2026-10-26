namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A record's anchors (M-7); each null when the record has none.</summary>
public sealed record RoleHolderScope(Guid? ProjectId, Guid? DepartmentId, Guid? ExternalEntityId);
