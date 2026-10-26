using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>The editable representation of an entity; the code is its business key and fixed at creation.</summary>
public sealed record ExternalEntityChanges(BilingualLabel Name, Guid EntityTypeItemId, Guid? SponsorUserId);
