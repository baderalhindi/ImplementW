namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>ADM-011/012 filters, ordered by code. <see cref="Text"/> matches code or either name, ignoring case.</summary>
public sealed record DepartmentQuery(bool? IsActive, Guid? ParentDepartmentId, string? Text, PageRequest Page);
