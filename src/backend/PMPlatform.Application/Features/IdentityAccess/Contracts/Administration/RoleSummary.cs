using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A row of ADM-006 Roles: one of the canonical R01–R08.</summary>
public sealed record RoleSummary(Guid Id, string Code, BilingualLabel Name, bool IsSystem, bool IsExternalEligible);
