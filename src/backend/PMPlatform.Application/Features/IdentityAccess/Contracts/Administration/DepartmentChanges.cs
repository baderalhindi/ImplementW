using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>The editable representation of a department; the code is its business key and fixed at creation.</summary>
public sealed record DepartmentChanges(BilingualLabel Name, Guid? ParentDepartmentId, string? DirectoryReference);
