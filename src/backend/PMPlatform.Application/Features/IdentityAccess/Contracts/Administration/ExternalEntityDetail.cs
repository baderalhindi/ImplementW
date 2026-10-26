using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>An external entity (ADR-013). Its people may sign in and act only while it is ACTIVE.</summary>
public sealed record ExternalEntityDetail(
    Guid Id,
    string Code,
    BilingualLabel Name,
    Guid EntityTypeItemId,
    ExternalEntityStatus Status,
    Guid? SponsorUserId,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
