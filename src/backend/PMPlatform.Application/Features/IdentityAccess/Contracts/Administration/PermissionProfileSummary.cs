using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

public sealed record PermissionProfileSummary(Guid Id, string Code, BilingualLabel Name, Guid BaseRoleId, string BaseRoleCode, bool IsShippedDefault);
