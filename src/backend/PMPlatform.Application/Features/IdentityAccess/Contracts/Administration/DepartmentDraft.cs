using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

public sealed record DepartmentDraft(string Code, BilingualLabel Name, Guid? ParentDepartmentId, string? DirectoryReference);
