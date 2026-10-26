using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A node of ADM-011 Organization Structure and a row of ADM-012 Departments.</summary>
public sealed record DepartmentSummary(Guid Id, string Code, BilingualLabel Name, Guid? ParentDepartmentId, bool IsActive);
