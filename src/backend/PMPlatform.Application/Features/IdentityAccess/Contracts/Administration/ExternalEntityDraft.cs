using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary><see cref="EntityTypeItemId"/> is an item of master data catalogue EXTERNAL_ENTITY_TYPE; the sponsor is an active internal user.</summary>
public sealed record ExternalEntityDraft(string Code, BilingualLabel Name, Guid EntityTypeItemId, Guid? SponsorUserId);
