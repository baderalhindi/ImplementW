using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>ADM-010 filters; newest start first.</summary>
public sealed record AccessRelationshipQuery(Guid? UserId, Guid? ProjectId, IReadOnlyCollection<AccessRelationshipStatus> Statuses, PageRequest Page);
