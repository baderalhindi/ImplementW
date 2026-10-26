using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A row of ADM-013 Entities.</summary>
public sealed record ExternalEntitySummary(Guid Id, string Code, BilingualLabel Name, Guid EntityTypeItemId, ExternalEntityStatus Status, Guid? SponsorUserId);
